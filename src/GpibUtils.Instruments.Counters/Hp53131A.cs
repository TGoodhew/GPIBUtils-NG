using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// Driver for the HP 53131A Universal Counter (225 MHz, three input channels) — a plain IEEE-488.2
    /// SCPI instrument. Measures frequency on a numbered channel and sets the channel-1 input impedance.
    /// The **canonical** 53131A, consolidating the two identical `GPIBUtils/HPDevices` copies (issue #21)
    /// and the SCPI reader in `HP3499Demo` (#5). Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>Measurement completion is the counter's IEEE-488.2 operation-complete → SRQ handshake
    /// (<c>*ESE 1</c> enables OPC in the event register, <c>*SRE 32</c> summarises it into the status byte,
    /// <c>:INIT;*OPC</c> arms + requests completion). Rather than hand-rolling the serial-poll wait, this
    /// driver drives it through the shared #43 <see cref="CompletionWaiter"/> via a
    /// <see cref="SessionStatusChannel"/> — the data-driven <see cref="StatusModel"/> below is the only
    /// instrument-specific completion knowledge, and it can later move verbatim into the #41 instrument DB.
    /// Uses the direct-bit flow (poll the Event-Summary bit) — the counter's status byte carries no
    /// separate request-service condition to arm. Timeouts stay generous for HP-IB bus-extender latency.</para>
    /// </summary>
    public sealed class Hp53131A : IFrequencyCounter
    {
        /// <summary>GPIB address of the 53131A — its documented factory-default GPIB address is 3 (53131A
        /// Programming Guide, "GPIB Address": "When the Counter is shipped from the factory … the address
        /// set to '3'"). Override with <c>--address</c>. Note: the legacy <c>HP3499Demo</c> source hardcoded
        /// <c>GPIB0::23::INSTR</c> (a bench value, not the factory default) — configure the bench's actual
        /// address via <c>config address set hp53131a …</c> rather than relying on this fallback. As always,
        /// never trust bus-scan discovery here — the HP-IB extenders make every address look present.</summary>
        public const string DefaultResource = "GPIB0::3::INSTR";

        /// <summary>Event-Summary bit (ESB) weight in the status byte — set when an enabled Standard-Event
        /// (here OPC) occurs. This is the completion signal the waiter polls for.</summary>
        private const int EventSummaryBit = 0x20;   // 32

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the operation-complete wait, ms. Generous (matches the legacy 20 s I/O
        /// timeout) so a slow 1 Hz-resolution measurement — or HP-IB bus-extender turnaround — always
        /// finishes first; a genuinely absent signal trips it and surfaces as a typed timeout.</summary>
        public int CompletionTimeoutMs { get; set; } = 20000;

        /// <summary>Serial-poll interval while waiting for completion, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp53131A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests). Note the SRQ
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

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();          // GPIB device clear — drop pending I/O and any latched SRQ
            Send("*RST");              // reset the counter
            Send("*CLS");              // clear status registers + error queue
            Send("*SRE 0");            // no service-request enable yet
            Send("*ESE 0");            // no standard-event enable yet
            Send(":STAT:PRES");        // preset the SCPI status subsystem
        }

        public void Reset() => Send("*RST");

        public void SetInputImpedance(CounterInputImpedance impedance) =>
            Send(impedance == CounterInputImpedance.Ohms50 ? "INP:IMP 50" : "INP:IMP 1E+6");

        /// <summary>Valid input-channel range for the 53131A (channel 3 requires the optional third input).</summary>
        private const int MinChannel = 1, MaxChannel = 3;

        public double MeasureFrequency(int channel)
        {
            if (channel < MinChannel || channel > MaxChannel)
                throw new ArgumentOutOfRangeException(nameof(channel), channel,
                    $"Channel must be {MinChannel}–{MaxChannel}.");

            // Configure this channel for a frequency measurement. The arm/mask/restore around INIT are
            // issued by the completion waiter from the status model below.
            Send("CONF:FREQ (@" + channel.ToString(CultureInfo.InvariantCulture) + ")");

            var channelSpi = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP53131A", "measureFrequency", CompletionTimeoutMs,
                channelSpi, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            switch (result.Outcome)
            {
                case CompletionOutcome.Completed:
                    return ParseFrequency(Query("FETCH?"));
                case CompletionOutcome.InstrumentError:
                    throw Hp53131AException.InstrumentError(channel, result.Message);
                default:   // TimedOut / Refused / NeedsDefinition — free the bus, then surface it
                    _session.Clear();
                    throw Hp53131AException.Timeout(channel, result.Message);
            }
        }

        /// <summary>
        /// The 53131A's operation-complete status model, shaped for the direct-bit
        /// <see cref="CompletionWaiter"/> flow. The enable mask is the IEEE-488.2 Service Request Enable
        /// (<c>*SRE {mask}</c>, cleared with <c>*SRE 0</c>); the measurement is armed by enabling OPC in the
        /// event register and triggering (<c>*ESE 1;:INIT;*OPC</c>), and completion is the Event-Summary bit
        /// asserting in the status byte. Kept here (not hardcoded in the waiter) so it can move to the #41
        /// instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "*SRE {mask}", ClearCommand = "*SRE 0" },
            Bits = new Dictionary<string, int> { ["operationComplete"] = EventSummaryBit },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["measureFrequency"] = new StatusOperation
                {
                    Arm = "*ESE 1;:INIT;*OPC",
                    ExpectBit = "operationComplete",
                    Restore = "*ESE 0"
                }
            }
        };

        /// <summary>Parses a 53131A frequency reading (scientific notation, Hz) to a double.</summary>
        internal static double ParseFrequency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 53131A frequency reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
                throw new FormatException($"Unrecognized 53131A frequency reading: '{raw}'.");
            return hz;
        }
    }
}
