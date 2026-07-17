using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 436A power meter — a pre-SCPI HP-IB instrument programmed with single-character codes,
    /// returning a fixed 14-character output data string. No <c>*IDN?</c> — <see cref="Identify"/> returns a
    /// descriptor. Reconstructed from the 436A Operating Manual, Section III (issue #110). Runs over any
    /// <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp436A : IPowerMeter
    {
        /// <summary>GPIB address of the 436A — factory default 13 (HP power-meter default). Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::13::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp436A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        /// <summary>The 436A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 436A Power Meter (no *IDN? — pre-SCPI HP-IB)";

        /// <summary>Device clear resets the meter to a known state.</summary>
        public void Initialize() => _session.Clear();

        /// <summary>Auto-zeroes the sensor (<c>Z</c>).</summary>
        public void ZeroAndCalibrate() => Send("Z");

        /// <summary>Reads power in dBm: sends <c>9+DI</c> (Auto range, cal-factor disable, dBm mode, Trigger
        /// immediate), then parses the 14-character output data string.</summary>
        public double MeasurePowerDbm() => ParseReading(Query("9+DI"));

        /// <summary>
        /// Parses a 436A 14-character output string (<c>status,mode,range,sign,4-digit mantissa,E,exp-sign,
        /// 2-digit exp</c>). The status character 'P' = measured-value-valid; anything else (over/under range,
        /// still auto-zeroing) is surfaced as an error. Also accepts a bare number.
        /// </summary>
        internal static double ParseReading(string raw)
        {
            var t = (raw ?? string.Empty).Trim();
            if (t.Length == 0) throw new FormatException("Empty 436A reading.");

            // Fixed format: status[0] mode[1] range[2] then the signed exponential number from index 3.
            if (t.Length >= 4 && (t[3] == '+' || t[3] == '-') && !char.IsDigit(t[0]))
            {
                char status = char.ToUpperInvariant(t[0]);
                if (status != 'P')
                    throw new InvalidOperationException(
                        $"436A status '{t[0]}' is not measured-value-valid (over/under-range or auto-zeroing).");
                var number = t.Substring(3);
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"Unrecognized 436A data string: '{raw}'.");
                return v;
            }

            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare)) return bare;
            throw new FormatException($"Unrecognized 436A reading: '{raw}'.");
        }
    }
}
