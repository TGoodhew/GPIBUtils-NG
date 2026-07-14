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
    /// Driver for the HP/Agilent E4418B EPM-series single-channel RF power meter — a plain SCPI instrument.
    /// Zeroes+calibrates the sensor (<c>:CAL1:ALL</c>), applies the cal factor for a carrier frequency
    /// (<c>:FREQ …MHZ</c>), then triggers a measurement and reads power in dBm (<c>:CONF1;:INIT;FETCh?</c>).
    /// Ported from <c>GPIBUtils/HPDevices</c> (issue #25). Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>The zero/cal and measurement both complete via the IEEE-488.2 <c>*ESE 1</c>/<c>*SRE 32</c>/<c>*OPC</c>
    /// → SRQ handshake, driven through the shared #43 <see cref="CompletionWaiter"/> (direct-bit flow) — the
    /// same engine the 53131A uses — rather than a hand-rolled serial-poll loop.</para>
    /// </summary>
    public sealed class HpE4418B : IPowerMeter
    {
        /// <summary>GPIB address of the E4418B. The EPM series has no fixed factory-default address stated in
        /// the manual (set from the front panel); the legacy driver's usage example and bench used
        /// <c>GPIB0::13::INSTR</c>. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::13::INSTR";

        /// <summary>Event-Summary bit (ESB) — the operation-complete signal the waiter polls for.</summary>
        private const int EventSummaryBit = 0x20;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for an operation-complete wait, ms (generous for the ~seconds cal + extender latency).</summary>
        public int CompletionTimeoutMs { get; set; } = 30000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        public HpE4418B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

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

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            Send("*SRE 0");
            Send("*ESE 0");
        }

        public void Reset() => Send("*RST");

        public void SetFrequencyMHz(double mhz) =>
            Send(":FREQ " + mhz.ToString("0.######", CultureInfo.InvariantCulture) + "MHZ");

        public void ZeroAndCalibrate()
        {
            var result = RunCompletion("zeroCal");
            if (result.Outcome != CompletionOutcome.Completed)
                throw new InvalidOperationException($"E4418B zero/cal did not complete — {result.Message}");
        }

        public double MeasurePowerDbm()
        {
            var result = RunCompletion("measure");
            if (result.Outcome != CompletionOutcome.Completed)
                throw new InvalidOperationException($"E4418B measurement did not complete — {result.Message}");
            return ParsePower(Query("FETCH?"));
        }

        private CompletionResult RunCompletion(string operation)
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            return CompletionWaiter.Wait(StatusModel(), "HPE4418B", operation, CompletionTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs);
        }

        /// <summary>The E4418B operation-complete status model for the direct-bit
        /// <see cref="CompletionWaiter"/> flow (Service Request Enable mask; OPC-armed operations).</summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "*SRE {mask}", ClearCommand = "*SRE 0" },
            Bits = new Dictionary<string, int> { ["operationComplete"] = EventSummaryBit },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["zeroCal"] = new StatusOperation { Arm = "*ESE 1;:CAL1:ALL;*OPC", ExpectBit = "operationComplete", Restore = "*ESE 0" },
                ["measure"] = new StatusOperation { Arm = "*ESE 1;:CONF1;:INIT;*OPC", ExpectBit = "operationComplete", Restore = "*ESE 0" }
            }
        };

        internal static double ParsePower(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty E4418B power reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized E4418B power reading: '{raw}'.");
            return v;
        }
    }
}
