using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 8508A Vector Voltmeter (100 kHz–2 GHz) — a tuned dual-channel RF receiver with an
    /// IEEE-488.2 mnemonic (SCPI-like) command set. Configures + triggers + reads vector measurements
    /// (channel A/B voltage/power, B/A ratio, B−A phase, transmission, group delay, SWR, reflection
    /// coefficient, admittance, impedance) via <c>MEASure?</c>. Built from the 8508A User Guide command set
    /// (issue #104 — re-scoped from the mislabeled "Fluke 8508A"). Runs over any
    /// <see cref="IInstrumentSession"/>. Implements <see cref="IVectorVoltmeter"/>.
    /// </summary>
    public sealed class Hp8508A : IVectorVoltmeter
    {
        /// <summary>GPIB address of the 8508A — <b>provisional</b>; the User Guide did not surface a numeric
        /// factory default. Confirm on the bench and override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::8::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8508A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            Send("FREQuency:BAND:AUTO ON");
        }

        public void SetFrequencyBandAuto(bool on) => Send("FREQuency:BAND:AUTO " + (on ? "ON" : "OFF"));

        public void SetAveragingCount(int count)
        {
            if (count < 0 || count > 10)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Averaging count must be 0-10 (2^count readings).");
            Send("AVERage:COUNt " + count.ToString(CultureInfo.InvariantCulture));
        }

        public double Measure(VectorMeasurement measurement) =>
            ParseScalar(Query("MEASure? " + measurement.Mnemonic()), measurement.Mnemonic());

        public IReadOnlyList<double> MeasureMany(params VectorMeasurement[] measurements)
        {
            if (measurements == null || measurements.Length == 0)
                throw new ArgumentException("At least one measurement is required.", nameof(measurements));
            var list = string.Join(",", measurements.Select(m => m.Mnemonic()));
            return ParseArray(Query("MEASure? " + list), list);
        }

        internal static double ParseScalar(string raw, string what)
        {
            var first = (raw ?? string.Empty).Split(',')[0].Trim();
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 8508A {what} reply: '{raw}'.");
            return ScpiReading.Guard(v, first, "8508A " + what);
        }

        internal static IReadOnlyList<double> ParseArray(string raw, string what)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException($"Empty 8508A {what} reply.");
            var values = raw.Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? ScpiReading.Guard(v, t, "8508A " + what)
                    : throw new FormatException($"Unrecognized 8508A {what} field: '{t}'."))
                .ToList();
            if (values.Count == 0) throw new FormatException($"No parseable values in 8508A {what} reply: '{raw}'.");
            return values;
        }
    }
}
