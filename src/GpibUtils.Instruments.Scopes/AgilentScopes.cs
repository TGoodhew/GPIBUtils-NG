using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// Shared driver for the Agilent/HP 546xx / Infiniium SCPI scope family — root-level run control
    /// (<c>:RUN</c>/<c>:STOP</c>/<c>:SINGle</c>/<c>:AUToscale</c>), <c>:CHANnel&lt;n&gt;:DISPlay</c>, and the
    /// <c>:MEASure</c> subsystem. Concrete models (54622A/54845A) differ only in default resource and channel
    /// count. Runs over any <see cref="IInstrumentSession"/> (issues #115/#116).
    /// </summary>
    public abstract class AgilentScope : IOscilloscope, IWaveformCapture
    {
        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private readonly int _channelCount;

        protected AgilentScope(IInstrumentSession session, int channelCount)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _channelCount = channelCount;
        }

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        private int Check(int ch)
        {
            if (ch < 1 || ch > _channelCount)
                throw new ArgumentOutOfRangeException(nameof(ch), ch, $"Channel must be 1-{_channelCount}.");
            return ch;
        }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("*CLS"); }

        public void Run() => Send(":RUN");
        public void Stop() => Send(":STOP");
        public void Single() => Send(":SINGle");
        public void AutoScale() => Send(":AUToscale");

        public void SetChannelDisplay(int channel, bool on) =>
            Send(":CHANnel" + Check(channel) + ":DISPlay " + (on ? "ON" : "OFF"));

        /// <summary>Peak-to-peak volts — shorthand for <see cref="Measure"/>.</summary>
        public double MeasureVpp(int channel) => Measure(channel, ScopeMeasurementType.PeakToPeak);

        /// <summary>Takes an automatic measurement (<c>:MEASure:&lt;kw&gt;? CHANnel&lt;n&gt;</c>).</summary>
        public double Measure(int channel, ScopeMeasurementType type) =>
            ParseReading(Query(":MEASure:" + Keyword(type) + "? CHANnel" + Check(channel)));

        private static string Keyword(ScopeMeasurementType type)
        {
            switch (type)
            {
                case ScopeMeasurementType.PeakToPeak: return "VPP";
                case ScopeMeasurementType.Maximum: return "VMAX";
                case ScopeMeasurementType.Minimum: return "VMIN";
                case ScopeMeasurementType.Amplitude: return "VAMPlitude";
                case ScopeMeasurementType.Mean: return "VAVerage";
                case ScopeMeasurementType.Rms: return "VRMS";
                case ScopeMeasurementType.Frequency: return "FREQuency";
                case ScopeMeasurementType.Period: return "PERiod";
                case ScopeMeasurementType.RiseTime: return "RISetime";
                case ScopeMeasurementType.FallTime: return "FALLtime";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Acquires and returns the channel's waveform (volts) via the SCPI <c>:WAVeform</c> subsystem:
        /// <c>:DIGitize</c> (acquire + completion gate), then ASCII <c>:WAVeform:DATA?</c>.</summary>
        public double[] CaptureWaveform(int channel)
        {
            Send(":DIGitize CHANnel" + Check(channel));
            Send(":WAVeform:SOURce CHANnel" + channel);
            Send(":WAVeform:FORMat ASCii");
            return ParseWaveform(Query(":WAVeform:DATA?"));
        }

        internal static double[] ParseWaveform(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new double[0];
            var list = new List<double>();
            foreach (var tok in raw.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            return list.ToArray();
        }

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty Agilent scope reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized Agilent scope reading: '{raw}'.");
            return v;
        }
    }

    /// <summary>HP/Agilent 54622A/54622D 2-channel oscilloscope (#115). GPIB (via N2757A module).</summary>
    public sealed class Hp54622A : AgilentScope
    {
        /// <summary>Default GPIB address 7 (the Programmer's Guide examples use select-code 707). Override
        /// with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";
        public Hp54622A(IInstrumentSession session) : base(session, 2) { }
    }

    /// <summary>Agilent 54845A Infiniium 4-channel oscilloscope (#116). GPIB/LAN. Some Infiniium mnemonics were
    /// not machine-extractable from the manual's figures — bench-confirm the exact keywords.</summary>
    public sealed class Hp54845A : AgilentScope
    {
        /// <summary>Default GPIB address 7 (Programmer's Guide examples use 707). Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";
        public Hp54845A(IInstrumentSession session) : base(session, 4) { }
    }
}
