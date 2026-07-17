using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 5005B Signature Multimeter — a 1983-era logic signature analyzer + general multimeter
    /// with a pre-SCPI mnemonic HP-IB command set (no <c>*IDN?</c>/<c>*OPC</c>/<c>*ESE</c>). Reconstructed from
    /// the 5005B Operating &amp; Service Manual, Section III remote-programming chapter (issue #112); runs over
    /// any <see cref="IInstrumentSession"/>. Implements the new <see cref="ISignatureAnalyzer"/> interface.
    ///
    /// <para><b>Completion — a #96 consumer.</b> There is no <c>*SRE</c>: SRQ is armed via a vendor "Service
    /// Request Mask" mnemonic <c>QMn</c> (a bitmask over data-ready = 1, probe-switch = 2, error = 4) and the
    /// status byte has an instrument-specific bit map (data-ready = 0x01, error = 0x04, SRQ flag = 0x40).
    /// <see cref="TriggerAndRead"/> drives the shared <see cref="CompletionWaiter"/> SRQ-edge flow with the
    /// <c>QM{mask}</c>/<c>QM0</c> enable — a custom (non-<c>*SRE</c>) enable command and custom bit table,
    /// both of which the data-driven <see cref="StatusModel"/> expresses directly. Timeouts stay generous for
    /// HP-IB bus-extender latency.</para>
    /// </summary>
    public sealed class Hp5005B : ISignatureAnalyzer
    {
        /// <summary>GPIB address of the 5005B — the manual factory default is 03 ("factory set to address 03").
        /// Override with <c>--address</c>; confirm against the rear-panel address switches. Never trust
        /// bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::3::INSTR";

        /// <summary>Status-byte bit weights (5005B Table 3-17). SRQ flag is the standard GPIB bit 0x40.</summary>
        private const int DataReadyBit = 1, ProbeSwitchBit = 2, ErrorBit = 4, BusyBit = 8,
                          LocalBit = 16, PowerOkBit = 32, SrqFlagBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the measurement-complete wait, ms.</summary>
        public int MeasureTimeoutMs { get; set; } = 30000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp5005B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command string sent through the driver, in order (for CLI echo / tests).</summary>
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

        public string Identify() => Query("ID");

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear — clears error, starts a fresh measurement
            Send("RS");         // reset to power-up defaults (F0,PC1,PT1,PP1,TD1,TC1,TQ1,PS0,QM0)
        }

        public void Reset() => Send("RS");

        public void SetFunction(SignatureFunction function) => Send("F" + (int)function);

        public void SetDataThreshold(LogicThreshold threshold) => Send("TD" + (int)threshold);

        public void SetClockPolarity(EdgePolarity polarity) => Send("PC" + (int)polarity);

        public void SetProbeSwitchEnabled(bool enabled) => Send(enabled ? "PS1" : "PS0");

        /// <summary>Reads the raw front-panel setup bytes (<c>SU</c>: function/polarity/threshold/mask nibbles).</summary>
        public string ReadSetupRaw() => Query("SU");

        public int ReadErrorCode()
        {
            var raw = Query("SE");
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? code : 0;
        }

        /// <summary>
        /// Arms the SRQ mask, waits for the measurement to complete (data-ready service request), and reads
        /// back the raw ASCII result. The mask/wait/disarm is run by the shared <see cref="CompletionWaiter"/>
        /// from <see cref="StatusModel"/>; the reading is then collected by addressing the instrument to talk.
        /// </summary>
        public string TriggerAndRead()
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP5005B", "measurement", MeasureTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    _history.Add("<read>");
                    return (_session.ReadString() ?? string.Empty).Trim();
                case CompletionOutcome.InstrumentError:
                    throw Hp5005BException.InstrumentError(result.Message);
                default:
                    _session.Clear();
                    throw Hp5005BException.Timeout(result.Message);
            }
        }

        public double Measure(SignatureFunction function)
        {
            SetFunction(function);
            var raw = TriggerAndRead();
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
            throw new FormatException($"Unparseable 5005B numeric reading: '{raw}' (function {function}).");
        }

        /// <summary>
        /// The 5005B's measurement-complete status model for the SRQ-edge <see cref="CompletionWaiter"/> flow:
        /// the vendor <c>QM{mask}</c>/<c>QM0</c> Service-Request-Mask enable (mask bits data-ready = 1,
        /// error = 4) plus the legacy 5005B status-byte bit table. Data-ready (0x01) is the armed completion
        /// condition; error (0x04) the fail bit; SRQ flag (0x40) the request-service bit. Kept here so it can
        /// move to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "QM{mask}", ClearCommand = "QM0" },
            ErrorBit = "error",
            RequestServiceBit = "srqFlag",
            Bits = new Dictionary<string, int>
            {
                ["dataReady"] = DataReadyBit,
                ["probeSwitch"] = ProbeSwitchBit,
                ["error"] = ErrorBit,
                ["busy"] = BusyBit,
                ["local"] = LocalBit,
                ["powerOk"] = PowerOkBit,
                ["srqFlag"] = SrqFlagBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                // No arm command — the instrument free-runs and self-triggers; the mask (data-ready|error)
                // and the busy-then-data-ready serial-poll cycle are enough to catch a fresh completion.
                ["measurement"] = new StatusOperation { ExpectBit = "dataReady" }
            }
        };
    }
}
