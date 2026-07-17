using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the Rigol DG1000Z-series dual-channel function/arbitrary waveform generator (DG1022Z/
    /// DG1032Z/DG1062Z) — a SCPI (IEEE-488.2) instrument. Implements <see cref="IFunctionGenerator"/> over any
    /// <see cref="IInstrumentSession"/>; the active channel is <see cref="SelectedChannel"/> (1 or 2, per the
    /// RigolDp832 multi-output pattern). Issue #99.
    ///
    /// <para><b>Bus note.</b> GPIB on this family is an option extended from the front USB-Host port via a
    /// USB-GPIB converter (not a native rear-panel bus) — so a GPIB session depends on that converter, and
    /// bus-scan discovery is even less reliable here. USB/LAN are the native transports.</para>
    /// </summary>
    public sealed class RigolDg1000Z : IFunctionGenerator
    {
        /// <summary>GPIB address of the DG1000Z — factory default 2 (<c>:SYSTem:COMMunicate:GPIB:ADDRess</c>,
        /// range 0-30). Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::2::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public RigolDg1000Z(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>The channel (1 or 2) that the <see cref="IFunctionGenerator"/> methods act on.</summary>
        public int SelectedChannel { get; set; } = 1;

        /// <summary>Every SCPI command sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

        private int Ch => SelectedChannel == 2 ? 2 : 1;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        public string Identify() => (_session.Query("*IDN?") ?? string.Empty).Trim();

        public void Initialize()
        {
            _session.Clear();
            Send("*CLS");
            Send("*RST");
        }

        public void SetWaveform(FunctionWaveform waveform) =>
            Send(":SOUR" + Ch + ":FUNC " + ShapeCode(waveform));

        public void SetFrequencyHz(double hertz) =>
            Send(":SOUR" + Ch + ":FREQ " + hertz.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetAmplitudeVpp(double voltsPeakToPeak) =>
            Send(":SOUR" + Ch + ":VOLT " + voltsPeakToPeak.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetOffsetVolts(double volts) =>
            Send(":SOUR" + Ch + ":VOLT:OFFS " + volts.ToString("0.######", CultureInfo.InvariantCulture));

        public void OutputOn() => Send(":OUTP" + Ch + " ON");

        public void OutputOff() => Send(":OUTP" + Ch + " OFF");

        private static string ShapeCode(FunctionWaveform w)
        {
            switch (w)
            {
                case FunctionWaveform.Sine: return "SIN";
                case FunctionWaveform.Square: return "SQU";
                case FunctionWaveform.Triangle: return "TRI";
                case FunctionWaveform.Ramp: return "RAMP";
                case FunctionWaveform.Pulse: return "PULS";
                case FunctionWaveform.Noise: return "NOIS";
                case FunctionWaveform.Dc: return "DC";
                case FunctionWaveform.Arbitrary: return "USER";
                default: throw new NotSupportedException($"Unsupported waveform {w}.");
            }
        }
    }
}
