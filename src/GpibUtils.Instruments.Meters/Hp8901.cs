using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the HP 8901A/8901B Modulation Analyzer — a pre-SCPI HP-IB instrument driven by concatenated
    /// function codes (no <c>*IDN?</c>). Presets, tunes to a carrier, then selects a measurement (AM/FM/…),
    /// triggers with settling (<c>T3</c>), and reads the 17-character exponential result. Reconstructed from
    /// the 8901 Operating Manual, Section III (issue #130); implements <see cref="IModulationAnalyzer"/>.
    /// Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>The AM (<c>M1</c>) and FM (<c>M2</c>) codes are manual-confirmed; the ΦM / RF-power / frequency
    /// codes (M3–M5) are a best-effort reconstruction — confirm at the bench. Completion here is the
    /// trigger-with-settling blocking read; the SF-22 SRQ-mask path (as on the 8903B) maps onto the shared
    /// Srq engine if async completion is wanted.</para>
    /// </summary>
    public sealed class Hp8901 : IModulationAnalyzer
    {
        /// <summary>GPIB address of the 8901 — the manual's examples address it at 14. NOTE the 8902A also
        /// defaults to 14; remap one on a shared bus. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::14::INSTR";

        /// <summary>Readings at or above this magnitude are the 8901's error-message sentinel, not data.</summary>
        public const double ErrorSentinel = 9.0e10;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8901(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 8901 predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 8901A/8901B Modulation Analyzer (no *IDN?)";

        public void Initialize() { _session.Clear(); Send("IP"); }

        /// <summary>Tunes to the carrier frequency in MHz using Automatic Operation (<c>AU &lt;f&gt; MZ</c>).</summary>
        public void TuneMHz(double megahertz) =>
            Send("AU " + megahertz.ToString("0.######", CultureInfo.InvariantCulture) + " MZ");

        /// <summary>Selects a measurement (<c>M1</c>…<c>M5</c>), triggers with settling (<c>T3</c>), and reads
        /// the result.</summary>
        public double Measure(ModulationMeasurementType type)
        {
            Send(Code(type) + " T3");
            _history.Add("<read>");
            return ParseReading(_session.ReadString());
        }

        private static string Code(ModulationMeasurementType type)
        {
            switch (type)
            {
                case ModulationMeasurementType.Am: return "M1";
                case ModulationMeasurementType.Fm: return "M2";
                case ModulationMeasurementType.PhaseModulation: return "M3";
                case ModulationMeasurementType.RfPower: return "M4";
                case ModulationMeasurementType.Frequency: return "M5";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Parses the 8901's 17-char implicit-decimal exponential output (parses directly as a
        /// float); a magnitude ≥ <see cref="ErrorSentinel"/> is the instrument's error sentinel.</summary>
        internal static double ParseReading(string raw)
        {
            var t = (raw ?? string.Empty).Trim();
            if (t.Length == 0) throw new FormatException("Empty 8901 reading.");
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 8901 reading: '{raw}'.");
            if (Math.Abs(v) >= ErrorSentinel)
                throw new InvalidOperationException($"8901 returned an error sentinel ({t}).");
            return v;
        }
    }
}
