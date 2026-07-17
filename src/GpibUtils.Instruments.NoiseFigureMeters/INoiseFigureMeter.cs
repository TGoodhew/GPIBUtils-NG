namespace GpibUtils.Instruments.NoiseFigureMeters
{
    /// <summary>
    /// A noise-figure meter (HP 8970B): sets a start/stop or fixed measurement frequency, selects an
    /// uncorrected-NF or corrected-NF+Gain mode, triggers a measurement and reads back noise figure (and gain).
    /// No existing interface expresses the dual NF+Gain result. Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #132 (P1 #85).
    /// </summary>
    public interface INoiseFigureMeter
    {
        /// <summary>The resource string this meter's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Identifies the instrument (the 8970B has no <c>*IDN?</c>; returns a descriptor).</summary>
        string Identify();

        /// <summary>Device clear + reset status to a known state.</summary>
        void Initialize();

        /// <summary>Sets the sweep start frequency, in MHz (<c>FA&lt;f&gt;EN</c>).</summary>
        void SetStartFrequencyMHz(double megahertz);

        /// <summary>Sets the sweep stop frequency, in MHz (<c>FB&lt;f&gt;EN</c>).</summary>
        void SetStopFrequencyMHz(double megahertz);

        /// <summary>Sets a fixed measurement frequency, in MHz (<c>FR&lt;f&gt;EN</c>).</summary>
        void SetFixedFrequencyMHz(double megahertz);

        /// <summary>Selects uncorrected noise figure (<c>M1</c>) or corrected NF+Gain (<c>M2</c>).</summary>
        void SetMode(NoiseFigureMode mode);

        /// <summary>Triggers one measurement (<c>T2</c>) and reads back the result (NF, and Gain in NF+Gain mode).</summary>
        NoiseFigureReading Measure();
    }

    /// <summary>Noise-figure measurement mode (the <c>M1</c>/<c>M2</c> codes).</summary>
    public enum NoiseFigureMode { NoiseFigure, NoiseFigureAndGain }

    /// <summary>A noise-figure measurement: noise figure (dB), and gain (dB, NaN when not in NF+Gain mode).</summary>
    public struct NoiseFigureReading
    {
        public double NoiseFigureDb { get; }
        public double GainDb { get; }

        public NoiseFigureReading(double noiseFigureDb, double gainDb)
        {
            NoiseFigureDb = noiseFigureDb;
            GainDb = gainDb;
        }

        public override string ToString() =>
            "NF=" + NoiseFigureDb.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) +
            " dB" + (double.IsNaN(GainDb) ? "" : ", Gain=" + GainDb.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) + " dB");
    }
}
