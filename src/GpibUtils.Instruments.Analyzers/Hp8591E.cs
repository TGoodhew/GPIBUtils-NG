using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Driver for the HP/Agilent 8591E spectrum analyzer — part of the HP 8590 D/E/L-series family — a
    /// pre-SCPI mnemonic HP-IB instrument (same command family as the 8566B/8568B era, NOT the 8560-series
    /// SCPI-compatible mode). Sets center frequency / span / bandwidths / sweep time, triggers a single sweep
    /// with the analyzer's legacy RQS-mask completion handshake, reads the trace (<c>TRA?</c>) and markers
    /// (<c>MKPK HI</c> / <c>MKF?</c> / <c>MKA?</c>). Reconstructed from the 8590 E/L-series Programmer's Guide
    /// (HP 08590-90235, issue #121); runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion — a legacy consumer of the #96 engine work.</b> This generation predates
    /// IEEE-488.2: there is no <c>*OPC</c>/<c>*ESE</c>/<c>*ESR</c>. Instead the analyzer has its own status
    /// byte with an <c>RQS &lt;mask&gt;</c> enable mask and reads that byte back with the <c>STB?</c> query
    /// (which, per the manual, clears it exactly as a serial poll would). <see cref="SingleSweep"/> therefore
    /// drives the shared <see cref="CompletionWaiter"/> SRQ-edge flow but configured (via
    /// <see cref="StatusModel.StatusQuery"/>) to read the status byte with <c>STB?</c> rather than a hardware
    /// serial poll, and with the 8590-family legacy bit table (end-of-sweep = 0x04, request-service = 0x40)
    /// rather than IEEE-488.2 semantics. Timeouts stay generous for HP-IB bus-extender latency.</para>
    ///
    /// <para>HP-IB only for v1; the manual documents an RS-232 transport that must poll <c>STB?</c> with no
    /// SRQ line — filed as a follow-up rather than built here.</para>
    /// </summary>
    public sealed class Hp8591E : ISpectrumAnalyzer
    {
        /// <summary>GPIB address of the 8591E — the documented default HP-IB address for the 8590 series is
        /// 18 ("The usual address for the spectrum analyzer is 18"). Override with <c>--address</c>; confirm
        /// against the unit's <c>CONFIG &gt; ANALYZER ADDRESS</c> softkey. Never trust bus-scan discovery on
        /// this bench (HP-IB extenders make every address look present).</summary>
        public const string DefaultResource = "GPIB0::18::INSTR";

        /// <summary>Status-byte bit weights (8590 E/L-series Programmer's Guide RQS-mask table). Request-service
        /// is the standard GPIB bit 0x40 the instrument sets on any enabled condition.</summary>
        private const int UnitsKeyBit = 2, EndOfSweepBit = 4, HardwareBrokenBit = 8,
                          CommandCompleteBit = 16, IllegalCommandBit = 32, RequestServiceBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the sweep-complete wait, ms. Generous so a long narrow-RBW sweep — or HP-IB
        /// bus-extender turnaround — always finishes first; a genuinely absent completion trips it and
        /// surfaces as a typed timeout.</summary>
        public int SweepTimeoutMs { get; set; } = 30000;

        /// <summary>Status-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp8591E(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests). The SRQ
        /// mask/arm/restore commands are issued by the <see cref="CompletionWaiter"/> straight onto the
        /// session, so they appear in the simulator's command log rather than here.</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        /// <summary>Reads the instrument identity (<c>ID?</c>). The 8590 series has no <c>*IDN?</c>; confirm
        /// <c>ID?</c> against the bench unit.</summary>
        public string Identify() => Query("ID?");

        public void Initialize()
        {
            _session.Clear();     // HP-IB device clear — drop pending I/O and any latched SRQ
            Send("IP");           // instrument preset
            Send("RQS 0");        // clear the service-request mask
        }

        public void Preset() => Send("IP");

        /// <summary>Sets the center frequency in Hz (<c>CF &lt;hz&gt; HZ</c>).</summary>
        public void SetCenterFrequencyHz(double hertz) => Send(FormatCommand("CF", hertz));

        /// <summary>Sets the center frequency in MHz.</summary>
        public void SetCenterFrequencyMHz(double mhz) => SetCenterFrequencyHz(mhz * 1e6);

        /// <summary>Sets the frequency span in Hz (<c>SP &lt;hz&gt; HZ</c>; 0 = zero span).</summary>
        public void SetSpanHz(double hertz) => Send(FormatCommand("SP", hertz));

        /// <summary>Sets the resolution bandwidth in Hz (<c>RB &lt;hz&gt; HZ</c>).</summary>
        public void SetResolutionBandwidthHz(double hertz) => Send(FormatCommand("RB", hertz));

        /// <summary>Sets the video bandwidth in Hz (<c>VB &lt;hz&gt; HZ</c>).</summary>
        public void SetVideoBandwidthHz(double hertz) => Send(FormatCommand("VB", hertz));

        /// <summary>Sets the sweep time in seconds (<c>ST &lt;s&gt; S</c>).</summary>
        public void SetSweepTimeSeconds(double seconds) =>
            Send("ST " + seconds.ToString("G6", CultureInfo.InvariantCulture) + " S");

        // Fixed notation (not G/scientific) so a frequency like 300 MHz is sent as "300000000 HZ".
        private static string FormatCommand(string mnemonic, double hertz) =>
            mnemonic + " " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ";

        /// <summary>
        /// Triggers a single sweep and blocks until the end-of-sweep RQS fires, driven by the shared
        /// <see cref="CompletionWaiter"/> (SRQ-edge flow, status read via <c>STB?</c>). The arm/mask/restore
        /// around <c>SNGLS;TS;</c> are issued by the waiter from <see cref="StatusModel"/>.
        /// </summary>
        public void SingleSweep()
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP8591E", "sweepComplete", SweepTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    return;
                case CompletionOutcome.InstrumentError:
                    throw Hp8591EException.InstrumentError(result.Message);
                default:   // TimedOut / Refused / NeedsDefinition — free the bus, then surface it
                    _session.Clear();
                    throw Hp8591EException.Timeout(result.Message);
            }
        }

        /// <summary>Reads the current trace amplitudes (<c>TRA?</c>), parsed from the comma-separated reply.</summary>
        public IReadOnlyList<double> ReadTrace() => Hp8560E.ParseTrace(Query("TRA?"));

        /// <summary>Puts the marker on the highest peak (<c>MKPK HI</c>) and returns its amplitude (<c>MKA?</c>).</summary>
        public double MarkerToPeakAmplitude()
        {
            Send("MKPK HI");
            return Hp8560E.ParseScalar(Query("MKA?"), "MKA?");
        }

        /// <summary>Reads the active marker's frequency in Hz (<c>MKF?</c>).</summary>
        public double MarkerFrequencyHz() => Hp8560E.ParseScalar(Query("MKF?"), "MKF?");

        /// <summary>Reads the active marker's amplitude (<c>MKA?</c>), analyzer amplitude units.</summary>
        public double MarkerAmplitude() => Hp8560E.ParseScalar(Query("MKA?"), "MKA?");

        /// <summary>
        /// The 8591E's sweep-complete status model, shaped for the SRQ-edge <see cref="CompletionWaiter"/>
        /// flow but with the two 8590-family / pre-488.2 traits the #96 engine work added: the status byte is
        /// read via the <c>STB?</c> query (<see cref="StatusModel.StatusQuery"/>) rather than a hardware
        /// serial poll, and the bit table is the legacy HP layout (end-of-sweep = 0x04 is the armed
        /// completion condition; illegal-command = 0x20 the fail bit; request-service = 0x40). The mask is
        /// armed with the <c>RQS {mask}</c> mnemonic. Kept here (not hardcoded in the waiter) so it can move
        /// to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            StatusQuery = new StatusQuerySpec { Command = "STB?" },   // #96: read the status byte by query
            SerialPoll = new SerialPollSpec { ClearsRqs = true },     // STB? clears the byte like a serial poll
            EnableMask = new EnableMaskSpec { SetCommand = "RQS {mask}", ClearCommand = "RQS 0" },
            ErrorBit = "illegalCommand",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["unitsKey"] = UnitsKeyBit,
                ["endOfSweep"] = EndOfSweepBit,
                ["hardwareBroken"] = HardwareBrokenBit,
                ["commandComplete"] = CommandCompleteBit,
                ["illegalCommand"] = IllegalCommandBit,
                ["requestService"] = RequestServiceBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["sweepComplete"] = new StatusOperation
                {
                    Arm = "SNGLS;TS;",
                    ExpectBit = "endOfSweep",
                    Restore = "CONTS;"
                }
            }
        };
    }
}
