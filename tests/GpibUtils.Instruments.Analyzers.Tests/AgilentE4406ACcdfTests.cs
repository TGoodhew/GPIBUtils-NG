using GpibUtils.Instruments.Analyzers;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Analyzers.Tests
{
    /// <summary>Tests the E4406A typed CCDF measurement (#12 follow-up) mapping the documented PSTatistic layout.</summary>
    public class AgilentE4406ACcdfTests
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
        public void Measure_ccdf_maps_avg_probability_and_papr()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                // PSTatistic: [0]=avg dBm, [1]=prob@avg %, ... [8]=PAPR dB, [9]=count.
                sim.SetResult(AgilentE4406A.CcdfRoot, new double[] { -8.2, 50.1, 10, 5, 3, 2, 1.5, 1, 11.7, 1000000 });
                var r = driver.MeasureCcdf(1000e6);
                Assert.Equal(-8.2, r.AveragePowerDbm, 3);
                Assert.Equal(50.1, r.ProbabilityAtAveragePercent, 3);
                Assert.Equal(11.7, r.PaprDb, 3);
                Assert.Contains(":SENSe:PSTatistic:COUNts 1000000", driver.History);
                Assert.Contains(":READ:PSTatistic?", driver.History);
            }
        }

        [Fact]
        public void Measure_ccdf_missing_fields_are_nan()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.SetResult(AgilentE4406A.CcdfRoot, new double[] { -8.2, 50.1 });   // short set
                var r = driver.MeasureCcdf(1000e6);
                Assert.Equal(-8.2, r.AveragePowerDbm, 3);
                Assert.Equal(50.1, r.ProbabilityAtAveragePercent, 3);
                Assert.True(double.IsNaN(r.PaprDb));
            }
        }
    }
}
