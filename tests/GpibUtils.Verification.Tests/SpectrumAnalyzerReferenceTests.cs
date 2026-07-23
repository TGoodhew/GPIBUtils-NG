using GpibUtils.Verification.References;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    public class SpectrumAnalyzerReferenceTests
    {
        [Fact]
        public void Power_reference_tunes_and_reads_the_peak_dbm()
        {
            var sa = new FakeSpectrumAnalyzer(peakDbm: -12.5, peakHz: 1_000_000_000);
            var reference = new SpectrumAnalyzerPowerReference(sa, null, "HP 8560E", spanHz: 2e6);

            Assert.Equal(ReferenceQuantity.RfPowerDbm, reference.Quantity);
            Assert.Equal("dBm", reference.Unit);

            reference.Prepare(new ReferencePoint { FrequencyMHz = 1000 });
            Assert.Equal(1_000_000_000, sa.LastCenterHz);   // tuned to 1000 MHz
            Assert.Equal(2e6, sa.LastSpanHz);

            Assert.Equal(-12.5, reference.Measure(), 6);
            Assert.Equal(1, sa.Sweeps);
            reference.Dispose();
        }

        [Fact]
        public void Frequency_reference_peaks_the_marker_and_reads_hz()
        {
            var sa = new FakeSpectrumAnalyzer(peakDbm: -20, peakHz: 2_000_050_000);
            var reference = new SpectrumAnalyzerFrequencyReference(sa, null, "HP 8591E");

            Assert.Equal(ReferenceQuantity.FrequencyHz, reference.Quantity);
            reference.Prepare(new ReferencePoint { FrequencyMHz = 2000 });
            Assert.Equal(2_000_050_000, reference.Measure(), 0);
            Assert.Equal(1, sa.Sweeps);
        }

        [Fact]
        public void Analyzer_power_reference_grades_a_signal_source()
        {
            // A source commanded to 0 dBm, an analyzer that reads -0.5 dBm: within a 1 dB tolerance.
            var sa = new FakeSpectrumAnalyzer(peakDbm: -0.5, peakHz: 1_000_000_000);
            var powerRef = new SpectrumAnalyzerPowerReference(sa, null, "HP 8560E");
            var results = new SignalSourceVerifier(new FakeSignalSource(), powerRef, null,
                new SignalSourceOptions { SettlingMs = 0, DefaultPowerToleranceDb = 1.0 })
                .Run(new[] { new SignalSourcePoint { FrequencyMHz = 1000, PowerDbm = 0 } });

            Assert.Equal("PASS", results[0].PowerVerdict);
            Assert.Equal(-0.5, results[0].PowerErrorDb, 6);
        }
    }
}
