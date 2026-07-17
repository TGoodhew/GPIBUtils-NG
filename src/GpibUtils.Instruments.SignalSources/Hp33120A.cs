using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP/Agilent 33120A 15 MHz Function / Arbitrary Waveform Generator — a SCPI (IEEE-488.2)
    /// instrument. Implements <see cref="IFunctionGenerator"/> over any <see cref="IInstrumentSession"/>.
    /// The core waveform/frequency/amplitude/offset setters are fire-and-forget SCPI writes (issue #106).
    /// </summary>
    public sealed class Hp33120A : IFunctionGenerator
    {
        /// <summary>GPIB address of the 33120A — factory default 10 (front-panel-settable, stored in NVRAM).
        /// Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::10::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp33120A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every SCPI command sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

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
            Send("VOLT:UNIT VPP");   // amplitude is programmed in Vpp
        }

        public void SetWaveform(FunctionWaveform waveform) => Send("FUNC:SHAP " + ShapeCode(waveform));

        public void SetFrequencyHz(double hertz) =>
            Send("FREQ " + hertz.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetAmplitudeVpp(double voltsPeakToPeak) =>
            Send("VOLT " + voltsPeakToPeak.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetOffsetVolts(double volts) =>
            Send("VOLT:OFFS " + volts.ToString("0.######", CultureInfo.InvariantCulture));

        /// <summary>The 33120A output cannot be disabled over the bus (it is always active — attenuate via
        /// amplitude instead), so this is a no-op documented for interface uniformity.</summary>
        public void OutputOn() { /* always enabled on the 33120A */ }

        /// <summary>No bus output-disable on the 33120A — see <see cref="OutputOn"/>. No-op.</summary>
        public void OutputOff() { /* not supported by the 33120A over HP-IB */ }

        private static string ShapeCode(FunctionWaveform w)
        {
            switch (w)
            {
                case FunctionWaveform.Sine: return "SIN";
                case FunctionWaveform.Square: return "SQU";
                case FunctionWaveform.Triangle: return "TRI";
                case FunctionWaveform.Ramp: return "RAMP";
                case FunctionWaveform.Noise: return "NOIS";
                case FunctionWaveform.Dc: return "DC";
                case FunctionWaveform.Arbitrary: return "USER";
                default: throw new NotSupportedException($"The 33120A does not support the {w} waveform.");
            }
        }
    }
}
