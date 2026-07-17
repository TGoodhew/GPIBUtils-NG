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
    /// Driver for the HP 3585A / 3585B low-frequency spectrum analyzer (10 Hz – 40.1 MHz) — a classic 1970s
    /// HP-IB instrument with a two-letter mnemonic / key-code language and no IEEE-488.2 common commands
    /// (no <c>*IDN?</c> / <c>*OPC</c>). Reconstructed from the 3585A/3585B Operating Manuals, Remote Operation
    /// section (issue #108); runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion — a "custom legacy vocabulary" consumer of the shared #43/#96 engine.</b> There is
    /// no RQS enable-mask register and no <c>*SRE</c>: an end-of-sweep service request is armed by enabling
    /// operation-complete SRQ with the <c>CQ</c> mnemonic (disabled by <c>CC</c>) and triggering a single
    /// sweep (<c>S2;T5;</c>). <see cref="SingleSweep"/> drives the shared <see cref="CompletionWaiter"/>
    /// SRQ-edge flow with the 3585 status-byte table (operation-complete = 0x08, request-service = 0x40) and
    /// the <c>CQ</c>/<c>CC</c> commands as the enable/disable — proving the engine hardcodes no <c>RQS</c>-style
    /// mask. Status is read by hardware serial poll. Keep timeouts generous (narrow-RBW sweeps are slow and
    /// HP-IB extenders add latency).</para>
    ///
    /// <para>This driver targets the 3585B operation-complete-SRQ path (also present, as documented, on the
    /// 3585A via the <c>T5</c> data-ready trigger — filed as a bench follow-up). Marker peak is found in
    /// software from the trace dump, as the manual excerpt did not capture a dedicated peak-search mnemonic.</para>
    /// </summary>
    public sealed class Hp3585 : ISpectrumAnalyzer
    {
        /// <summary>GPIB address of the 3585 — factory-default HP-IB address 11 on both the 3585A (rear-panel
        /// switches) and 3585B ("Bus Address" softkey). Override with <c>--address</c>; verify against the
        /// bench unit. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::11::INSTR";

        /// <summary>Status-byte bit weights (3585B status word, superset of the 3585A). Request-service is the
        /// standard GPIB bit 0x40.</summary>
        private const int SyntaxErrorBit = 1, DataReadyBit = 2, KeyPressedBit = 4,
                          OperationCompleteBit = 8, LimitFailBit = 16, RequestServiceBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the sweep-complete wait, ms. Generous — worst-case narrow-RBW sweeps plus
        /// HP-IB extender turnaround.</summary>
        public int SweepTimeoutMs { get; set; } = 60000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp3585(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
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

        /// <summary>The 3585 has no <c>*IDN?</c>; returns a fixed descriptor (identify the unit by nameplate).</summary>
        public string Identify() => "HP 3585A/3585B Spectrum Analyzer (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();     // HP-IB device clear
            Send("PR");           // instrument preset (bus equivalent of the front-panel Preset)
            Send("CC");           // disable operation-complete SRQ (clean known state)
        }

        public void Preset() => Send("PR");

        /// <summary>Sets the center frequency in Hz (<c>CF &lt;hz&gt; HZ</c>).</summary>
        public void SetCenterFrequencyHz(double hertz) => Send(FormatCommand("CF", hertz));

        /// <summary>Sets the center frequency in MHz.</summary>
        public void SetCenterFrequencyMHz(double mhz) => SetCenterFrequencyHz(mhz * 1e6);

        /// <summary>Sets the frequency span in Hz (<c>FS &lt;hz&gt; HZ</c>).</summary>
        public void SetSpanHz(double hertz) => Send(FormatCommand("FS", hertz));

        /// <summary>Sets the start frequency in Hz (<c>FA &lt;hz&gt; HZ</c>).</summary>
        public void SetStartFrequencyHz(double hertz) => Send(FormatCommand("FA", hertz));

        /// <summary>Sets the stop frequency in Hz (<c>FB &lt;hz&gt; HZ</c>).</summary>
        public void SetStopFrequencyHz(double hertz) => Send(FormatCommand("FB", hertz));

        /// <summary>Sets the resolution bandwidth in Hz (<c>RB &lt;hz&gt; HZ</c>).</summary>
        public void SetResolutionBandwidthHz(double hertz) => Send(FormatCommand("RB", hertz));

        /// <summary>Sets the video bandwidth in Hz (<c>VB &lt;hz&gt; HZ</c>).</summary>
        public void SetVideoBandwidthHz(double hertz) => Send(FormatCommand("VB", hertz));

        /// <summary>Sets the reference level in dB (<c>RL &lt;v&gt; DB</c>).</summary>
        public void SetReferenceLevelDb(double db) =>
            Send("RL " + db.ToString("G6", CultureInfo.InvariantCulture) + " DB");

        /// <summary>Sets the sweep time in seconds (<c>ST &lt;s&gt; SC</c>).</summary>
        public void SetSweepTimeSeconds(double seconds) =>
            Send("ST " + seconds.ToString("G6", CultureInfo.InvariantCulture) + " SC");

        private static string FormatCommand(string mnemonic, double hertz) =>
            mnemonic + " " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ";

        /// <summary>
        /// Triggers a single sweep and blocks until the operation-complete SRQ fires, driven by the shared
        /// <see cref="CompletionWaiter"/> (SRQ-edge flow, hardware serial poll). The <c>CQ</c>/<c>CC</c> enable
        /// and <c>S2;T5;</c> arm are issued by the waiter from <see cref="StatusModel"/>.
        /// </summary>
        public void SingleSweep()
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP3585", "sweepComplete", SweepTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    return;
                case CompletionOutcome.InstrumentError:
                    throw Hp3585Exception.InstrumentError(result.Message);
                default:
                    _session.Clear();
                    throw Hp3585Exception.Timeout(result.Message);
            }
        }

        /// <summary>Reads trace A (<c>D3</c> dump, comma-separated), parsed to amplitudes.</summary>
        public IReadOnlyList<double> ReadTrace() => Hp8560E.ParseTrace(Query("D3"));

        /// <summary>Places the marker on the highest point of the trace (found in software from the <c>D3</c>
        /// dump) and returns its amplitude. The 3585's dedicated peak-search mnemonic was not captured in the
        /// manual excerpt, so the peak is computed from the trace rather than by an on-instrument search.</summary>
        public double MarkerToPeakAmplitude() => ReadTrace().Max();

        /// <summary>Reads the active marker's frequency in Hz from the <c>D2</c> dump (marker frequency +
        /// amplitude); returns the leading frequency field.</summary>
        public double MarkerFrequencyHz() => Hp8560E.ParseScalar(Query("D2"), "D2");

        /// <summary>Reads the active marker's amplitude from the <c>D1</c> dump (marker amplitude).</summary>
        public double MarkerAmplitude() => Hp8560E.ParseScalar(Query("D1"), "D1");

        /// <summary>
        /// The 3585's sweep-complete status model, shaped for the SRQ-edge <see cref="CompletionWaiter"/> flow
        /// with the legacy 3585 bit table and the <c>CQ</c>/<c>CC</c> operation-complete-SRQ enable in place of
        /// a numeric RQS mask (proving the engine hardcodes no <c>RQS</c> command). Status is read by hardware
        /// serial poll. Kept here (not in the waiter) so it can move to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "CQ", ClearCommand = "CC" },  // op-complete SRQ enable/disable
            ErrorBit = "syntaxError",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["syntaxError"] = SyntaxErrorBit,
                ["dataReady"] = DataReadyBit,
                ["keyPressed"] = KeyPressedBit,
                ["operationComplete"] = OperationCompleteBit,
                ["limitFail"] = LimitFailBit,
                ["requestService"] = RequestServiceBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["sweepComplete"] = new StatusOperation
                {
                    Arm = "S2;T5;",                 // single sweep + delayed trigger
                    ExpectBit = "operationComplete"
                }
            }
        };
    }
}
