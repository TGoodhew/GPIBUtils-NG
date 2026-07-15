using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// An in-memory model of an Agilent E4406A VSA for use with <see cref="SimulatedGpibProvider"/>, rich
    /// enough to drive the <see cref="AgilentE4406A"/> driver end to end with no hardware. It tracks the mode
    /// (Basic), single/continuous, and center frequency, and answers the measurement verbs
    /// (<c>:READ/MEASure/FETCh:&lt;root&gt;?</c>) with a per-root scalar set plus <c>:SENSe:FREQuency:CENTer?</c>
    /// and <c>:SYSTem:ERRor?</c>. <c>*IDN?</c> falls back to the simulator's common-command handling.
    /// </summary>
    public sealed class AgilentE4406ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly Dictionary<string, double[]> _results =
            new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every command the analyzer was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public string Mode { get; private set; }
        public bool Continuous { get; private set; } = true;
        public double CenterFrequencyHz { get; private set; }

        /// <summary>The error string the next <c>:SYSTem:ERRor?</c> returns (and clears). Default = no error.</summary>
        public string PendingError { get; set; } = "+0,\"No error\"";

        public AgilentE4406ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Agilent Technologies,E4406A,US40000123,A.08.10",
                WriteObserver = Apply,
                Responder = Respond
            };
            // Sensible default scalar sets so a READ returns something plausible.
            SetResult(AgilentE4406A.ChannelPowerRoot, new double[] { -10.5, -83.2 });
            SetResult(AgilentE4406A.AcpRoot, new double[] { -10.5, -55.1, -56.3 });
        }

        /// <summary>Sets the scalar set a measurement root returns (e.g. "CHPower" -&gt; [power, psd]).</summary>
        public void SetResult(string root, double[] scalars) => _results[root] = scalars;

        private void Apply(string command)
        {
            var raw = command.TrimEnd('\r', '\n');
            if (raw.Length == 0) return;
            _commands.Add(raw);
            var upper = raw.ToUpperInvariant();
            if (upper.IndexOf('?') >= 0) return;   // queries carry no state change

            if (upper.StartsWith(":INST") && upper.Contains(":SEL"))
            {
                var m = Regex.Match(upper, @":SEL(?:ECT)?\s+(\S+)");
                if (m.Success) Mode = m.Groups[1].Value;
                return;
            }
            if (upper.StartsWith(":INIT") && upper.Contains(":CONT"))
            {
                Continuous = !upper.Contains("OFF");
                return;
            }
            if (upper.StartsWith(":SENS") && upper.Contains(":FREQ") && upper.Contains(":CENT"))
            {
                var m = Regex.Match(upper, @"([-+0-9.]+(?:E[-+]?[0-9]+)?)");
                if (m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    CenterFrequencyHz = f;
                return;
            }
        }

        private string Respond(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            var upper = raw.ToUpperInvariant();

            if (upper.StartsWith(":SENS") && upper.Contains(":FREQ") && upper.Contains(":CENT"))
                return CenterFrequencyHz.ToString("G17", CultureInfo.InvariantCulture);
            if (upper.StartsWith(":SYST") && upper.Contains(":ERR"))
            {
                var e = PendingError; PendingError = "+0,\"No error\""; return e;
            }

            // Measurement verbs: :READ:<root>?, :MEASure:<root>?, :FETCh:<root>?
            var m = Regex.Match(upper, @":(?:READ|MEAS(?:URE)?|FETC(?:H)?):([A-Z]+?)(\d*)\?");
            if (m.Success)
            {
                var root = m.Groups[1].Value;
                if (_results.TryGetValue(root, out var scalars))
                    return string.Join(",", scalars.Select(v => v.ToString("G6", CultureInfo.InvariantCulture)));
                return "0";   // unknown root -> a single benign scalar
            }
            return null;   // *IDN? handled by the simulator defaults
        }
    }
}
