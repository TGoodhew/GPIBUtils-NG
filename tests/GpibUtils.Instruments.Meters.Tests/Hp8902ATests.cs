using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>
    /// Drives the <see cref="Hp8902A"/> driver against a simulated 8902A over the standard transport, so
    /// assertions exercise the real write / settled-read / serial-poll path with no hardware.
    /// </summary>
    public class Hp8902ATests
    {
        private static (Hp8902A driver, Hp8902ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8902ASimulatedDevice();
            provider.Add(Hp8902A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8902A.DefaultResource);
            // Zero the settle delays so the settled-read path runs instantly under test.
            var driver = new Hp8902A(session) { ZeroSettleMs = 0, CalSettleMs = 0, CalibrateSettleMs = 0, SettleMilliseconds = 0 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_a_measuring_receiver()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IMeasuringReceiver>(driver);
        }

        [Fact]
        public void Initialize_clears_then_presets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal("IP", Assert.Single(driver.History));
                Assert.Contains("IP", sim.Commands);
            }
        }

        // ---- Tuned RF Level (attenuation) setup ---------------------------------

        [Fact]
        public void BeginAttenuation_direct_manual_average_sends_expected_sequence()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginAttenuationMeasurement(3000, MeasurementRegime.Direct, 0);
                Assert.Equal<string>(new[] { "S4", "27.0SP", "3000MZ", "4.4SP", "LG", "1.0SP", "32.1SP", "22.37SP" },
                    driver.History);
                Assert.Equal("S4", sim.Mode);
                Assert.Equal(3000, sim.TunedMHz);
                Assert.False(sim.OffsetMode);
                Assert.Equal("4.4SP", sim.Detector);
                Assert.True(sim.StatusUnmasked);
            }
        }

        [Fact]
        public void BeginAttenuation_converted_enters_frequency_offset_with_LO()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginAttenuationMeasurement(12000, MeasurementRegime.Converted, 17120.53);
                Assert.Contains("27.3SP17120.53MZ", driver.History);
                Assert.True(sim.OffsetMode);
            }
        }

        [Fact]
        public void BeginAttenuation_synchronous_selects_narrowband_detector()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginAttenuationMeasurement(3000, MeasurementRegime.Direct, 0, TrflDetector.Synchronous);
                Assert.Contains("4.0SP", driver.History);
                Assert.Equal("4.0SP", sim.Detector);
            }
        }

        [Fact]
        public void BeginAttenuation_trackMode_uses_329SP_not_detector_or_LG()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginAttenuationMeasurement(12000, MeasurementRegime.Converted, 17120.53,
                    detector: TrflDetector.Synchronous, trackMode: true);
                Assert.Contains("32.9SP", driver.History);
                Assert.DoesNotContain("4.0SP", driver.History);
                Assert.DoesNotContain("4.4SP", driver.History);
                Assert.DoesNotContain("LG", driver.History);
                Assert.True(sim.TrackMode);
            }
        }

        // ---- settled reads ------------------------------------------------------

        [Fact]
        public void ReadRelativeDb_returns_the_settled_level()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = -42.5;
                Assert.Equal(-42.5, driver.ReadRelativeDb(), 6);
            }
        }

        [Fact]
        public void ReadRfPowerDbm_converts_watts_to_dBm()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginRfPowerMeasurement(3000, MeasurementRegime.Direct, 0);
                Assert.Contains("M4", driver.History);
                Assert.Contains("37.0SP", driver.History);
                sim.Reading = 1e-3;                       // 1 mW
                Assert.Equal(0.0, driver.ReadRfPowerDbm(), 6);   // 0 dBm
            }
        }

        [Fact]
        public void ReadSignalFrequencyMHz_scales_Hz_to_MHz()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = 3_000_000_000;             // 3 GHz in Hz
                Assert.Equal(3000, driver.ReadSignalFrequencyMHz(), 3);
                Assert.Equal("M5", sim.Mode);
            }
        }

        // ---- error / recal paths ------------------------------------------------

        [Fact]
        public void Read_with_recal_pending_throws_uncal()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.RecalPending = true;
                var ex = Assert.Throws<Hp8902AException>(() => driver.ReadRelativeDb());
                Assert.True(ex.IsUncal);
            }
        }

        [Fact]
        public void RecalRequested_reflects_the_status_bit()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.BeginRangeCalibration();          // T0 arms the status byte
                Assert.False(driver.RecalRequested());
                sim.RecalPending = true;
                driver.BeginRangeCalibration();          // re-arm with RECAL set
                Assert.True(driver.RecalRequested());
            }
        }

        [Fact]
        public void Calibrate_raises_instrument_error_when_the_error_bit_sets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorPending = true;
                Assert.Throws<Hp8902AException>(() => driver.Calibrate());
            }
        }

        // ---- reading parser -----------------------------------------------------

        [Theory]
        [InlineData("-42.5", -42.5)]
        [InlineData("0", 0.0)]
        [InlineData("1.234E-03", 1.234e-3)]
        public void ParseReading_parses_numeric_values(string raw, double expected)
        {
            Assert.Equal(expected, Hp8902A.ParseReading(raw), 9);
        }

        [Fact]
        public void ParseReading_error_sentinel_throws_with_code()
        {
            // +900000NNNNE+01 => code = (value - 9e10)/1000. Encode Error 96.
            double sentinel = 9e10 + 96 * 1000.0;
            var ex = Assert.Throws<Hp8902AException>(() =>
                Hp8902A.ParseReading(sentinel.ToString("0", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.Equal(96, ex.Code);
        }

        [Theory]
        [InlineData("CCCC")]
        [InlineData("AAAA")]
        public void ParseReading_uncal_fill_throws_uncal(string fill)
        {
            var ex = Assert.Throws<Hp8902AException>(() => Hp8902A.ParseReading(fill));
            Assert.True(ex.IsUncal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseReading_empty_read_is_retriable(string raw)
        {
            var ex = Assert.Throws<Hp8902AException>(() => Hp8902A.ParseReading(raw));
            Assert.True(ex.IsEmpty);
        }

        [Fact]
        public void ParseReading_uncal_fill_surfaces_through_a_read()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ReadingOverride = "CCCC";
                var ex = Assert.Throws<Hp8902AException>(() => driver.ReadRfPowerDbm());
                Assert.True(ex.IsUncal);
            }
        }
    }
}
