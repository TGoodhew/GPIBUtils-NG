using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of a Keysight E4438C ESG for use with <see cref="SimulatedGpibProvider"/>, rich
    /// enough to drive the <see cref="KeysightE4438C"/> driver end to end with no hardware. It tracks the
    /// carrier (frequency/power/RF/modulation), the reference and ARB state, and the set of downloaded ARB
    /// segments, and answers the queries (<c>:FREQ:FIXed?</c> ± MIN/MAX, <c>:POW:LEVel?</c> ± MIN/MAX,
    /// <c>:RAD:ARB:SCLock:RATE? MAX</c>, <c>:ROSC:SOURce?</c>, <c>:SYST:ERRor?</c>). <c>*IDN?</c> / <c>*OPC?</c>
    /// fall back to the simulator's built-in common-command handling.
    /// </summary>
    public sealed class KeysightE4438CSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly HashSet<string> _volatileSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _nonVolatileSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every command the generator was sent (writes and queries), in order (for assertions).
        /// The binary ARB download appears as its Latin-1 message text (prefix + block + payload bytes).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public double FrequencyHz { get; private set; } = 1e9;
        public double PowerDbm { get; private set; } = -10;
        public bool RfOn { get; private set; }
        public bool ModulationOn { get; private set; }
        public bool ArbOn { get; private set; }
        public bool ReferenceAuto { get; private set; }
        public string SelectedWaveform { get; private set; }

        public double MinFrequencyHz { get; set; } = 250e3;
        public double MaxFrequencyHz { get; set; } = 6e9;
        public double MinPowerDbm { get; set; } = -136;
        public double MaxPowerDbm { get; set; } = 20;
        public double MaxSampleClockHz { get; set; } = 100e6;

        /// <summary>Reference source reported by <c>:ROSCillator:SOURce?</c> (INT / EXT).</summary>
        public string ReferenceSource { get; set; } = "INT";

        /// <summary>The error string the next <c>:SYSTem:ERRor?</c> returns (and clears). Default = no error.</summary>
        public string PendingError { get; set; } = "+0,\"No error\"";

        /// <summary>Names of segments downloaded to volatile (WFM1) memory.</summary>
        public IReadOnlyCollection<string> VolatileSegments => _volatileSegments;

        /// <summary>Names of segments copied to non-volatile (NVWFM) storage.</summary>
        public IReadOnlyCollection<string> NonVolatileSegments => _nonVolatileSegments;

        public KeysightE4438CSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Agilent Technologies,E4438C,MY40000123,C.05.20",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var raw = command.TrimEnd('\r', '\n');
            if (raw.Length == 0) return;
            _commands.Add(raw);
            var upper = raw.ToUpperInvariant();
            if (upper.IndexOf('?') >= 0) return;   // queries carry no state change

            if (Match(upper, @":FREQ(?:UENCY)?:FIX(?:ED)?\s+([-+0-9.EE]+)", out var f)) { FrequencyHz = f; return; }
            if (Match(upper, @":POW(?:ER)?:LEV(?:EL)?\s+([-+0-9.EE]+)", out var p)) { PowerDbm = p; return; }
            if (upper.StartsWith(":OUTP") && upper.Contains(":MOD")) { ModulationOn = IsOn(upper); return; }
            if (upper.StartsWith(":OUTP")) { RfOn = IsOn(upper); return; }
            if (upper.Contains(":ROSC") && upper.Contains(":AUTO")) { ReferenceAuto = IsOn(upper); return; }
            if (upper.Contains(":RAD") && upper.Contains(":ARB:STAT")) { ArbOn = IsOn(upper); return; }
            if (upper.Contains(":RAD") && upper.Contains(":ARB:WAV"))
            {
                var m = Regex.Match(raw, "WFM1:([^\"]+)");
                if (m.Success) SelectedWaveform = m.Groups[1].Value;
                return;
            }
            if (upper.StartsWith(":MEM") && upper.Contains(":DATA"))
            {
                var m = Regex.Match(raw, "\"WFM1:([^\"]+)\"");
                if (m.Success) _volatileSegments.Add(m.Groups[1].Value);
                return;
            }
            if (upper.StartsWith(":MEM") && upper.Contains(":COPY"))
            {
                // :MEMory:COPY "WFM1:seg","NVWFM:seg"  or the reverse
                var targets = Regex.Matches(raw, "\"(WFM1|NVWFM):([^\"]+)\"");
                if (targets.Count == 2)
                {
                    var dest = targets[1];
                    var name = dest.Groups[2].Value;
                    if (dest.Groups[1].Value.Equals("NVWFM", StringComparison.OrdinalIgnoreCase)) _nonVolatileSegments.Add(name);
                    else _volatileSegments.Add(name);
                }
                return;
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();

            if (upper.StartsWith(":FREQ") && upper.Contains(":FIX"))
                return Fmt(RangeArg(upper, FrequencyHz, MinFrequencyHz, MaxFrequencyHz));
            if (upper.StartsWith(":POW") && upper.Contains(":LEV"))
                return Fmt(RangeArg(upper, PowerDbm, MinPowerDbm, MaxPowerDbm));
            if (upper.Contains(":ARB:SCL") && upper.Contains("MAX"))
                return Fmt(MaxSampleClockHz);
            if (upper.StartsWith(":ROSC") && upper.Contains(":SOUR"))
                return ReferenceSource;
            if (upper.StartsWith(":SYST") && upper.Contains(":ERR"))
            {
                var e = PendingError; PendingError = "+0,\"No error\""; return e;
            }
            return null;   // *IDN? / *OPC? handled by the simulator's common-command defaults
        }

        /// <summary>Picks the base value, or the min/max when the query carries a MIN/MAX argument.</summary>
        private static double RangeArg(string upper, double value, double min, double max)
        {
            if (upper.Contains("MAX")) return max;
            if (upper.Contains("MIN")) return min;
            return value;
        }

        /// <summary>An ON/OFF command is ON unless it carries the OFF keyword (avoids matching "modulatiON").</summary>
        private static bool IsOn(string upper) => !upper.Contains("OFF");

        private static bool Match(string s, string pattern, out double value)
        {
            var m = Regex.Match(s, pattern);
            if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            value = 0;
            return false;
        }

        private static string Fmt(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
    }
}
