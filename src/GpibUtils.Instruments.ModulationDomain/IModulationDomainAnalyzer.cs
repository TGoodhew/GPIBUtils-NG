using System.Collections.Generic;

namespace GpibUtils.Instruments.ModulationDomain
{
    /// <summary>
    /// A modulation-domain analyzer (HP 53310A): measures frequency-vs-time or time-interval-vs-time (an
    /// array of values across a captured record), optionally post-processed into a histogram — a data model
    /// that fits neither a spectrum analyzer, a frequency counter, nor an oscilloscope. Runs over any
    /// <see cref="Visa.IInstrumentSession"/>. New interface for issue #113 (P1 #87).
    /// </summary>
    public interface IModulationDomainAnalyzer
    {
        /// <summary>The resource string this analyzer's session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>Reads the instrument identity (<c>*IDN?</c>).</summary>
        string Identify();

        /// <summary>Device clear + reset to a known state.</summary>
        void Initialize();

        /// <summary>Configures a standard measurement on the given input channel (<c>:CONFigure</c>).</summary>
        void Configure(ModulationMeasurement measurement, int channel = 1);

        /// <summary>Initiates the configured measurement and reads back the result array (<c>:READ?</c>).</summary>
        IReadOnlyList<double> Read();
    }

    /// <summary>The standard modulation-domain measurement types (the HP 53310A <c>:CONFigure</c> functions).</summary>
    public enum ModulationMeasurement
    {
        FrequencyVsTime,        // :CONFigure:XTIMe:FREQuency<ch>
        TimeIntervalVsTime,     // :CONFigure:XTIMe:TINTerval
        FrequencyHistogram,     // :CONFigure:HISTogram:FREQuency<ch>
        TimeIntervalHistogram   // :CONFigure:HISTogram:TINTerval
    }
}
