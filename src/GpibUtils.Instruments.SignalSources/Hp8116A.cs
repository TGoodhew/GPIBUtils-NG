using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8116A Programmable Pulse/Function Generator (50 MHz, 1982) — a pre-SCPI HP-IB
    /// instrument with a legacy comma-delimited mnemonic language (no <c>*IDN?</c>/<c>*OPC</c>/<c>*ESE</c>).
    /// Reconstructed from the 8116A Operating &amp; Service Manual, Section III (issue #118); implements
    /// <see cref="IFunctionGenerator"/> over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>Parameter sets are synchronous writes (no OPC/settle handshake); the only async event is
    /// SRQ-on-error, surfaced here via <see cref="ReadStatusByte"/> / <see cref="ReadError"/>. The frequency/
    /// amplitude/offset/duty/width mnemonics are from the manual and reliable; the <b>waveform-select mnemonic
    /// was not captured in the manual excerpt</b>, so <see cref="SetWaveform"/> throws rather than invent a
    /// code — set the waveform on the front panel or confirm the mnemonic at the bench (#118).</para>
    /// </summary>
    public sealed class Hp8116A : IFunctionGenerator
    {
        /// <summary>GPIB address of the 8116A — factory default 16 (rear-panel HP-IB ADDRESS switch, read at
        /// power-up / <c>LCL</c>). Override with <c>--address</c>. Never trust bus-scan discovery behind
        /// HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::16::INSTR";

        /// <summary>Status-byte bit weights (8116A Table 3-5). Bit 7 (0x40) is the SRQ summary bit.</summary>
        private const int TimingErrorBit = 1, ProgrammingErrorBit = 2, SyntaxErrorBit = 4,
                          SystemFailureBit = 8, ServiceRequestBit = 64;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8116A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every mnemonic program string sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>The 8116A has no <c>*IDN?</c>; returns a fixed descriptor (identify via the front panel).</summary>
        public string Identify() => "HP 8116A Pulse/Function Generator (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear (DCL) — loads the ROM default parameter set
            OutputOff();
        }

        /// <summary>Not supported: the 8116A waveform-select mnemonic was not captured in the manual excerpt
        /// (#118). Confirm the code at the bench, then wire this up. Frequency/amplitude/offset work.</summary>
        public void SetWaveform(FunctionWaveform waveform) =>
            throw new NotSupportedException(
                "HP 8116A waveform-select mnemonic is not confirmed from the manual (#118); set the waveform " +
                "on the front panel or confirm the code at the bench. Frequency/amplitude/offset are supported.");

        public void SetFrequencyHz(double hertz) =>
            Send("FRQ " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " HZ");

        public void SetAmplitudeVpp(double voltsPeakToPeak) =>
            Send("AMP " + voltsPeakToPeak.ToString("0.######", CultureInfo.InvariantCulture) + " V");

        public void SetOffsetVolts(double volts) =>
            Send("OFS " + volts.ToString("0.######", CultureInfo.InvariantCulture) + " V");

        /// <summary>Sets the duty cycle in percent (<c>DTY &lt;n&gt; %</c>).</summary>
        public void SetDutyCyclePercent(double percent) =>
            Send("DTY " + percent.ToString("0.######", CultureInfo.InvariantCulture) + " %");

        /// <summary>Enables the output (<c>D0</c> — the Disable toggle off). Confirm the D0/D1 polarity at bench.</summary>
        public void OutputOn() => Send("D0");

        /// <summary>Disables the output (<c>D1</c> — the Disable toggle on). Confirm the D0/D1 polarity at bench.</summary>
        public void OutputOff() => Send("D1");

        /// <summary>Reads the status byte by serial poll (see the fault bits above).</summary>
        public int ReadStatusByte() => _session.SerialPoll().Value;

        /// <summary>Interrogates the current error string (<c>IERR</c>), e.g. "WIDTH ERROR"; empty when none.</summary>
        public string ReadError() => (_session.Query("IERR") ?? string.Empty).Trim();

        /// <summary>True if the status byte shows any fault (timing/programming/syntax/system-failure).</summary>
        public bool HasFault() =>
            (ReadStatusByte() & (TimingErrorBit | ProgrammingErrorBit | SyntaxErrorBit | SystemFailureBit)) != 0;
    }
}
