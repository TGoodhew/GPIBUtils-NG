using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.NoiseFigureMeters
{
    /// <summary>
    /// Driver for the HP 8970B Noise Figure Meter — a pre-SCPI HP-IB instrument driven by concatenated
    /// mnemonics (no <c>*IDN?</c>). Sets a fixed or start/stop frequency, selects uncorrected NF (<c>M1</c>)
    /// or corrected NF+Gain (<c>M2</c>), triggers a measurement (<c>T2</c>) and reads the 12-byte ASCII
    /// record <c>sDDDDDEsNN</c> (which parses directly as a float; magnitudes ≥ <see cref="ErrorSentinel"/>
    /// are the instrument's error sentinel). Reconstructed from the 8970B Operating Manual, Section III and
    /// its worked calibration example (issue #132); implements <see cref="INoiseFigureMeter"/>.
    ///
    /// <para>Frequencies are entered in MHz per the manual's own examples (<c>FA20ENFB100EN</c>). Completion
    /// here is the trigger-with-hold blocking read (<c>T1</c> hold then <c>T2</c> trigger-execute); the
    /// <c>RM&lt;n&gt;EN</c> SRQ-mask path used for calibrate-complete maps onto the shared Srq engine when
    /// async completion is wanted. Confirm the read format / mnemonics at the bench (Needs Verification).</para>
    /// </summary>
    public sealed class Hp8970B : INoiseFigureMeter
    {
        /// <summary>GPIB address of the 8970B — the manual's calibration example addresses it as "Nfm"; 8 is
        /// the common HP default. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::8::INSTR";

        /// <summary>Readings at or above this magnitude are the 8970B's error sentinel (≥ 9×10^10), not data.</summary>
        public const double ErrorSentinel = 9.0e10;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private NoiseFigureMode _mode = NoiseFigureMode.NoiseFigure;

        public Hp8970B(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 8970B predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 8970B Noise Figure Meter (no *IDN?)";

        /// <summary>Device clear, hold trigger (<c>T1</c>), and reset status (<c>RS</c>).</summary>
        public void Initialize() { _session.Clear(); Send("T1"); Send("RS"); }

        public void SetStartFrequencyMHz(double megahertz) => Send("FA" + Mhz(megahertz) + "EN");

        public void SetStopFrequencyMHz(double megahertz) => Send("FB" + Mhz(megahertz) + "EN");

        public void SetFixedFrequencyMHz(double megahertz) => Send("FR" + Mhz(megahertz) + "EN");

        /// <summary>Selects uncorrected NF (<c>M1</c>) or corrected NF+Gain (<c>M2</c>).</summary>
        public void SetMode(NoiseFigureMode mode)
        {
            _mode = mode;
            Send(mode == NoiseFigureMode.NoiseFigureAndGain ? "M2" : "M1");
        }

        /// <summary>Triggers one measurement (<c>T2</c>) and reads back NF; in NF+Gain mode reads Gain too.</summary>
        public NoiseFigureReading Measure()
        {
            Send("T2");
            _history.Add("<read>");
            double nf = ParseReading(_session.ReadString());
            double gain = double.NaN;
            if (_mode == NoiseFigureMode.NoiseFigureAndGain)
            {
                _history.Add("<read>");
                gain = ParseReading(_session.ReadString());
            }
            return new NoiseFigureReading(nf, gain);
        }

        private static string Mhz(double megahertz) =>
            megahertz.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>Parses the 8970B's 12-byte exponential record <c>sDDDDDEsNN</c> (parses directly as a
        /// float); a magnitude ≥ <see cref="ErrorSentinel"/> is the instrument's error sentinel.</summary>
        internal static double ParseReading(string raw)
        {
            var t = (raw ?? string.Empty).Trim();
            if (t.Length == 0) throw new FormatException("Empty 8970B reading.");
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 8970B reading: '{raw}'.");
            if (Math.Abs(v) >= ErrorSentinel)
                throw new InvalidOperationException($"8970B returned an error sentinel ({t}).");
            return v;
        }
    }
}
