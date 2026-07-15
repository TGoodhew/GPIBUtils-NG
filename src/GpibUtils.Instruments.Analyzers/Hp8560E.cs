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
    /// Driver for the HP/Agilent 8560E portable microwave spectrum analyzer (30 Hz – 2.9/6.5/13.2/26.5/50 GHz)
    /// — a mnemonic HP-IB instrument. Sets the center frequency / span / bandwidths / sweep time, triggers a
    /// single sweep with the analyzer's operation-complete SRQ handshake, reads the trace (<c>TRA?</c>), and
    /// reads markers (<c>MKPK HI</c> / <c>MKF?</c> / <c>MKA?</c>). Reconstructed from the 8560E Programming
    /// Guide (issue #13); the legacy <c>DLPBits</c> app only loaded DLP downloadable programs, so the sweep
    /// mnemonics here come from the manual and must be confirmed at the bench. Runs over any
    /// <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion — the flagship #43 consumer.</b> The 8560-series RQS mask and the read-back status
    /// byte share one layout (Programming Guide Table 7-9): request-service is bit 0x40 — set on every SRQ,
    /// NOT an error. So <see cref="SingleSweep"/> arms <c>RQS 16</c> (command-complete) and issues
    /// <c>SNGLS;TS;</c>, then the shared <see cref="CompletionWaiter"/> runs the robust <b>SRQ-edge</b> flow
    /// (wait for the sweep to go busy, then accept request-service as completion) via a
    /// <see cref="SessionStatusChannel"/>. The data-driven <see cref="StatusModel"/> below is the only
    /// instrument-specific completion knowledge and can move verbatim into the #41 instrument DB. Timeouts
    /// stay generous for HP-IB bus-extender latency; <c>DONE?</c> is the non-SRQ alternative handshake.</para>
    /// </summary>
    public sealed class Hp8560E : ISpectrumAnalyzer
    {
        /// <summary>GPIB address of the 8560E — its documented factory-default HP-IB address is 18 (8560E
        /// Programming Guide). Override with <c>--address</c>. Never trust bus-scan discovery on this bench
        /// (HP-IB extenders make every address look present).</summary>
        public const string DefaultResource = "GPIB0::18::INSTR";

        /// <summary>Status-byte bit weights (8560E Programming Guide, Table 7-9).</summary>
        private const int TriggerBit = 1, MessageBit = 2, EndOfSweepBit = 4,
                          CommandCompleteBit = 16, ErrorBit = 32, RequestServiceBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the sweep-complete wait, ms. Generous so a long sweep — or HP-IB
        /// bus-extender turnaround — always finishes first; a genuinely absent completion trips it and
        /// surfaces as a typed timeout.</summary>
        public int SweepTimeoutMs { get; set; } = 30000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp8560E(IInstrumentSession session) =>
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

        // Fixed notation (not G/scientific) so a frequency like 1.5 GHz is sent as "1500000000 HZ".
        private static string FormatCommand(string mnemonic, double hertz) =>
            mnemonic + " " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ";

        /// <summary>
        /// Triggers a single sweep and blocks until the operation-complete SRQ fires, driven by the shared
        /// #43 <see cref="CompletionWaiter"/> (SRQ-edge flow). The arm/mask/restore around <c>TS</c> are
        /// issued by the waiter from <see cref="StatusModel"/>.
        /// </summary>
        public void SingleSweep()
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP8560E", "sweepComplete", SweepTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    return;
                case CompletionOutcome.InstrumentError:
                    throw Hp8560EException.InstrumentError(result.Message);
                default:   // TimedOut / Refused / NeedsDefinition — free the bus, then surface it
                    _session.Clear();
                    throw Hp8560EException.Timeout(result.Message);
            }
        }

        /// <summary>Reads the current trace amplitudes (<c>TRA?</c>), parsed from the comma-separated reply.</summary>
        public IReadOnlyList<double> ReadTrace() => ParseTrace(Query("TRA?"));

        /// <summary>Puts the marker on the highest peak (<c>MKPK HI</c>) and returns its amplitude (<c>MKA?</c>).</summary>
        public double MarkerToPeakAmplitude()
        {
            Send("MKPK HI");
            return ParseScalar(Query("MKA?"), "MKA?");
        }

        /// <summary>Reads the active marker's frequency in Hz (<c>MKF?</c>).</summary>
        public double MarkerFrequencyHz() => ParseScalar(Query("MKF?"), "MKF?");

        /// <summary>Reads the active marker's amplitude (<c>MKA?</c>), analyzer amplitude units.</summary>
        public double MarkerAmplitude() => ParseScalar(Query("MKA?"), "MKA?");

        /// <summary>
        /// The 8560E's sweep-complete status model, shaped for the robust SRQ-edge <see cref="CompletionWaiter"/>
        /// flow. The enable mask is the analyzer's Request-Service mask (<c>RQS {mask}</c> / <c>RQS 0</c>); the
        /// sweep is armed by <c>SNGLS;TS;</c> (single sweep + take sweep) and completion is the request-service
        /// bit (0x40) asserting after the sweep goes busy. Command-complete (0x10) is the expect (armed)
        /// condition; error (0x20) is the fail bit. Kept here (not hardcoded in the waiter) so it can move to
        /// the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "RQS {mask}", ClearCommand = "RQS 0" },
            ErrorBit = "error",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["trigger"] = TriggerBit,
                ["message"] = MessageBit,
                ["endOfSweep"] = EndOfSweepBit,
                ["commandComplete"] = CommandCompleteBit,
                ["error"] = ErrorBit,
                ["requestService"] = RequestServiceBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["sweepComplete"] = new StatusOperation
                {
                    Arm = "SNGLS;TS;",
                    ExpectBit = "commandComplete",
                    Restore = "CONTS;"
                }
            }
        };

        internal static IReadOnlyList<double> ParseTrace(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 8560E trace (TRA?) response.");
            var values = new List<double>();
            foreach (var token in raw.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length == 0) continue;
                if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"Unrecognized 8560E trace point: '{t}'.");
                values.Add(v);
            }
            if (values.Count == 0)
                throw new FormatException($"No parseable points in 8560E trace: '{raw}'.");
            return values;
        }

        internal static double ParseScalar(string raw, string what)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException($"Empty 8560E {what} response.");
            // Marker queries may return "<value> HZ" or "<value> DBM"; take the leading number.
            var first = raw.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (first == null || !double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 8560E {what} response: '{raw}'.");
            return v;
        }
    }
}
