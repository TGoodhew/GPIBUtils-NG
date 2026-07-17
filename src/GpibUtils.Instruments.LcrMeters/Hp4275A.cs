using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.LcrMeters
{
    /// <summary>
    /// Driver for the HP (YHP) 4275A Multi-Frequency LCR Meter (1979, 10 kHz–10 MHz) — a bridge-type impedance
    /// analyzer with HP-IB Option 101 and a pre-SCPI 2–3 character program-code language (no <c>*IDN?</c>/
    /// <c>*OPC</c>/<c>*ESE</c>). Reconstructed from the 4275A Operating Manual, Section III (issue #109); runs
    /// over any <see cref="IInstrumentSession"/>. First implementer of <see cref="ILcrMeter"/>.
    ///
    /// <para><b>Completion — a #96 consumer.</b> The 4275A has a fully custom pre-488.2 status byte (no
    /// <c>*SRE</c> enable-mask register); the only arm is the <c>I1</c>/<c>I0</c> Data-Ready-SRQ toggle.
    /// <see cref="Measure"/> configures + triggers via the shared <see cref="CompletionWaiter"/> SRQ-edge flow
    /// with the <c>I1</c>/<c>I0</c> enable (a custom, non-<c>*SRE</c> command) and the 4275A bit table
    /// (data-ready = bit 1, error = bit 4, RQS = bit 7 / 0x40), then reads the Format-A output. Timeouts stay
    /// generous for HP-IB bus-extender latency.</para>
    /// </summary>
    public sealed class Hp4275A : ILcrMeter
    {
        /// <summary>GPIB address of the 4275A — <b>provisional</b>. The factory address is set by the A22S1
        /// switch and the manual's Figure 3-23 factory setting could not be read from the scan; the manual's
        /// sample programs use address 17, used here pending bench confirmation of the physical switch.
        /// Override with <c>--address</c>. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::17::INSTR";

        /// <summary>Status-byte bit weights (4275A Figure 3-24). RQS is the standard GPIB bit 0x40.</summary>
        private const int DataReadyBit = 1, ProgramErrorBit = 2, ZeroOrSelfTestDoneBit = 4,
                          ErrorBit = 8, RequestServiceBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the measurement-complete wait, ms.</summary>
        public int MeasureTimeoutMs { get; set; } = 30000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp4275A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every program string sent through the driver, in order (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>The 4275A has no <c>*IDN?</c>; returns a fixed descriptor (identify by the front panel).</summary>
        public string Identify() => "HP 4275A Multi-Frequency LCR Meter (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("T3");         // HOLD / MANUAL trigger — armed for triggered single measurements
        }

        public void SetPrimaryParameter(LcrParameter parameter) => Send("A" + (int)parameter);

        public void SetCircuitMode(LcrCircuitMode mode) => Send("C" + (int)mode);

        public void SetTestFrequency(LcrFrequency frequency) => Send("F" + (int)frequency);

        public void ZeroOpen() => Send("Z0");

        public void ZeroShort() => Send("ZS");

        /// <summary>
        /// Ensures HOLD/MANUAL trigger mode then runs one triggered measurement: the shared
        /// <see cref="CompletionWaiter"/> arms the Data-Ready SRQ (<c>I1</c>), fires Execute (<c>E</c>), waits
        /// for the data-ready service request, then the Format-A output is read and parsed.
        /// </summary>
        public LcrReading Measure()
        {
            Send("T3");   // ensure HOLD/MANUAL so E triggers exactly one measurement
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP4275A", "measurement", MeasureTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    _history.Add("<read A,B>");
                    return ParseReading(_session.ReadString());
                case CompletionOutcome.InstrumentError:
                    throw Hp4275AException.InstrumentError(result.Message);
                default:
                    _session.Clear();
                    throw Hp4275AException.Timeout(result.Message);
            }
        }

        /// <summary>
        /// Parses a 4275A "Format A" reading into (primary, secondary). Format A is a fixed-field
        /// mode/frequency/status/function/value string for Display A, comma, then Display B. The exact field
        /// layout needs bench confirmation (#109); this extracts the first two numeric values, which are the
        /// Display A and Display B readings.
        /// </summary>
        internal static LcrReading ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 4275A Format-A response.");
            var numbers = Regex.Matches(raw, @"[-+]?\d+(\.\d+)?([eE][-+]?\d+)?")
                .Cast<Match>()
                .Select(m => double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToList();
            if (numbers.Count < 2)
                throw new FormatException($"Could not parse two values from 4275A response: '{raw}'.");
            return new LcrReading(numbers[0], numbers[1]);
        }

        /// <summary>
        /// The 4275A's measurement-complete status model for the SRQ-edge <see cref="CompletionWaiter"/> flow:
        /// the <c>I1</c>/<c>I0</c> Data-Ready-SRQ enable (there is no <c>*SRE</c> mask register) and the fully
        /// custom 4275A status-byte bit table. Data-ready (bit 1) is the armed completion condition; error
        /// (bit 4) the fail bit; RQS (0x40) the request-service bit. The Execute (<c>E</c>) is the arm. Kept
        /// here so it can move to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "I1", ClearCommand = "I0" },  // Data-Ready SRQ enable/disable
            ErrorBit = "error",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["dataReady"] = DataReadyBit,
                ["programError"] = ProgramErrorBit,
                ["zeroOrSelfTestDone"] = ZeroOrSelfTestDoneBit,
                ["error"] = ErrorBit,
                ["requestService"] = RequestServiceBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["measurement"] = new StatusOperation { Arm = "E", ExpectBit = "dataReady" }
            }
        };
    }
}
