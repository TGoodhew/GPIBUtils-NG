using System;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Drives the <see cref="AgilentE4406A"/> driver against a simulated E4406A over the standard transport.</summary>
    public class AgilentE4406ATests
    {
        private static (AgilentE4406A driver, AgilentE4406ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new AgilentE4406ASimulatedDevice();
            provider.Add(AgilentE4406A.DefaultResource, sim.Instrument);
            var session = provider.Open(AgilentE4406A.DefaultResource);
            return (new AgilentE4406A(session), sim, session);
        }

        [Fact]
        public void Identify_returns_idn()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("E4406A", driver.Identify());
        }

        [Fact]
        public void Default_address_is_manual_eighteen()
        {
            Assert.Equal("GPIB0::18::INSTR", AgilentE4406A.DefaultResource);
        }

        [Fact]
        public void Initialize_selects_basic_single()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal("BASIC", sim.Mode);
                Assert.False(sim.Continuous);
                Assert.Contains(":INSTrument:SELect BASIC", driver.History);
                Assert.Contains(":INITiate:CONTinuous OFF", driver.History);
            }
        }

        [Fact]
        public void Center_frequency_round_trips()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetCenterFrequencyMHz(1000);
                Assert.Equal(1e9, sim.CenterFrequencyHz, 0);
                Assert.Equal(1e9, driver.GetCenterFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Continuous_toggle()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetContinuous(); Assert.True(sim.Continuous);
                driver.SetSingleMeasurement(); Assert.False(sim.Continuous);
            }
        }

        [Fact]
        public void Channel_power_reads_scalars()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult(AgilentE4406A.ChannelPowerRoot, new double[] { -12.3, -80.4 });
                var r = driver.MeasureChannelPower(1e9, spanHz: 5e6, integrationBandwidthHz: 4e6);
                Assert.Equal(-12.3, r.TotalPowerDbm, 3);
                Assert.Equal(-80.4, r.PowerSpectralDensityDbmHz, 3);
                Assert.Contains(":SENSe:CHPower:FREQuency:SPAN 5000000 Hz", driver.History);
                Assert.Contains(":SENSe:CHPower:BANDwidth:INTegration 4000000 Hz", driver.History);
                Assert.Contains(":READ:CHPower?", driver.History);
            }
        }

        [Fact]
        public void Channel_power_missing_field_is_nan()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult(AgilentE4406A.ChannelPowerRoot, new double[] { -12.3 });
                var r = driver.MeasureChannelPower(1e9);
                Assert.Equal(-12.3, r.TotalPowerDbm, 3);
                Assert.True(double.IsNaN(r.PowerSpectralDensityDbmHz));
            }
        }

        [Fact]
        public void Acp_reads_scalars_and_has_no_span()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult(AgilentE4406A.AcpRoot, new double[] { -10.5, -55.1, -56.3 });
                var s = driver.MeasureAcp(1e9, integrationBandwidthHz: 3.84e6);
                Assert.Equal(3, s.Length);
                Assert.Equal(-10.5, s[0], 3);
                Assert.Contains(":READ:ACP?", driver.History);
                Assert.DoesNotContain(driver.History, c => c.Contains("ACP:FREQuency:SPAN"));
            }
        }

        [Fact]
        public void Generic_read_measure_fetch_verbs()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult("SPECtrum", new double[] { 1, 2, 3 });
                Assert.Equal(new double[] { 1, 2, 3 }, driver.Read("SPECtrum"));
                Assert.Equal(new double[] { 1, 2, 3 }, driver.Measure("SPECtrum"));
                Assert.Equal(new double[] { 1, 2, 3 }, driver.Fetch("SPECtrum"));
                Assert.Contains(":READ:SPECtrum?", driver.History);
                Assert.Contains(":MEASure:SPECtrum?", driver.History);
                Assert.Contains(":FETCh:SPECtrum?", driver.History);
            }
        }

        [Fact]
        public void Read_with_result_index_appends_n()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult("SPECtrum", new double[] { 9 });
                driver.Read("SPECtrum", 4);
                Assert.Contains(":READ:SPECtrum4?", driver.History);
            }
        }

        [Fact]
        public void Configure_sets_up_without_initiating()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Configure(AgilentE4406A.ChannelPowerRoot);
                Assert.Contains(":CONFigure:CHPower", driver.History);
            }
        }

        [Fact]
        public void Parse_scalars_tolerates_whitespace_and_quotes()
        {
            var s = AgilentE4406A.ParseScalars(" -10.5 , \"-83.2\" , notanumber , 5 ");
            Assert.Equal(new double[] { -10.5, -83.2, 5 }, s);
        }

        [Fact]
        public void Parse_scalars_on_empty_is_empty()
        {
            Assert.Empty(AgilentE4406A.ParseScalars(""));
            Assert.Empty(AgilentE4406A.ParseScalars(null));
        }

        [Fact]
        public void Error_query_reads_and_clears()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = "-113,\"Undefined header\"";
                Assert.Contains("Undefined header", driver.GetError());
                Assert.Contains("No error", driver.GetError());
            }
        }

        [Fact]
        public void Empty_root_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentException>(() => driver.Read(""));
        }
    }
}
