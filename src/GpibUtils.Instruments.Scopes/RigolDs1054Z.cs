using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// Driver for the Rigol DS1054Z 4-channel digital oscilloscope — a SCPI instrument. Controls
    /// acquisition (run/stop/single/autoscale), per-channel display, and reads automatic measurements and
    /// the timebase. Ported from the <c>GPIBUtils/OtherDevices/DS1054Z</c> waveform-viewer app (issue #27).
    /// Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>The DS1054Z is a USB/LXI instrument (no GPIB); <see cref="DefaultResource"/> is the legacy app's
    /// LXI resource. The provider model is transport-neutral, so the driver is unchanged over any session.</para>
    /// </summary>
    public sealed class RigolDs1054Z : IOscilloscope, IWaveformCapture
    {
        /// <summary>Default VISA resource of the DS1054Z — the legacy app's LXI address (the scope has no
        /// GPIB port; it is USB/LAN). Override with <c>--address</c> for the bench's actual IP.</summary>
        public const string DefaultResource = "TCPIP0::192.168.1.145::inst0::INSTR";

        /// <summary>Number of analog channels.</summary>
        public const int ChannelCount = 4;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public RigolDs1054Z(IInstrumentSession session) =>
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

        private static int Check(int channel)
        {
            if (channel < 1 || channel > ChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, $"Channel must be 1–{ChannelCount}.");
            return channel;
        }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*CLS");
        }

        public void Reset() => Send("*RST");

        public void Run() => Send(":RUN");
        public void Stop() => Send(":STOP");
        public void Single() => Send(":SINGle");
        public void AutoScale() => Send(":AUToscale");

        public void SetChannelDisplay(int channel, bool on) =>
            Send($":CHANnel{Check(channel)}:DISPlay {(on ? "ON" : "OFF")}");

        /// <summary>Reads the main timebase scale in seconds/div (<c>:TIMebase:MAIN:SCALe?</c>).</summary>
        public double TimebaseScaleSeconds() => ParseReading(Query(":TIMebase:MAIN:SCALe?"));

        public double MeasureVpp(int channel) => MeasureItem("VPP", channel);

        /// <summary>Measures peak voltage max on a channel (volts).</summary>
        public double MeasureVmax(int channel) => MeasureItem("VMAX", channel);

        /// <summary>Measures frequency on a channel (Hz).</summary>
        public double MeasureFrequency(int channel) => MeasureItem("FREQ", channel);

        /// <summary>Takes an automatic measurement of the given type (<c>:MEASure:ITEM?</c>).</summary>
        public double Measure(int channel, ScopeMeasurementType type) => MeasureItem(ItemCode(type), channel);

        private static string ItemCode(ScopeMeasurementType type)
        {
            switch (type)
            {
                case ScopeMeasurementType.PeakToPeak: return "VPP";
                case ScopeMeasurementType.Maximum: return "VMAX";
                case ScopeMeasurementType.Minimum: return "VMIN";
                case ScopeMeasurementType.Amplitude: return "VAMP";
                case ScopeMeasurementType.Mean: return "VAVG";
                case ScopeMeasurementType.Rms: return "VRMS";
                case ScopeMeasurementType.Frequency: return "FREQ";
                case ScopeMeasurementType.Period: return "PER";
                case ScopeMeasurementType.RiseTime: return "RTIM";
                case ScopeMeasurementType.FallTime: return "FTIM";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Acquires and returns the channel's waveform (volts) via the SCPI <c>:WAVeform</c> subsystem:
        /// <c>:STOP</c> (completion gate), NORMal/ASCii, then <c>:WAVeform:DATA?</c>.</summary>
        public double[] CaptureWaveform(int channel)
        {
            Send(":STOP");
            Send(":WAVeform:SOURce CHANnel" + Check(channel));
            Send(":WAVeform:MODE NORMal");
            Send(":WAVeform:FORMat ASCii");
            return ParseWaveform(Query(":WAVeform:DATA?"));
        }

        internal static double[] ParseWaveform(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new double[0];
            // Rigol ASCII :WAV:DATA? may carry a TMC block header "#9<9 digits>"; skip it if present.
            var body = raw;
            if (body.Length > 2 && body[0] == '#' && char.IsDigit(body[1]))
            {
                int n = body[1] - '0';
                int skip = 2 + n;
                if (body.Length > skip) body = body.Substring(skip);
            }
            var list = new List<double>();
            foreach (var tok in body.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    list.Add(v);
            return list.ToArray();
        }

        /// <summary>Reads an automatic measurement item on a channel (<c>:MEASure:ITEM? &lt;item&gt;,CHANnel{n}</c>).</summary>
        public double MeasureItem(string item, int channel) =>
            ParseReading(Query($":MEASure:ITEM? {item},CHANnel{Check(channel)}"));

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty DS1054Z reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized DS1054Z reading: '{raw}'.");
            return v;
        }
    }
}
