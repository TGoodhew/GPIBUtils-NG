using GpibUtils.Instruments.Analyzers;
using GpibUtils.Verification.References;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    public class TransmitterTesterReferenceTests
    {
        [Fact]
        public void Reads_channel_power_total_dbm_at_the_prepared_carrier()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new AgilentE4406ASimulatedDevice();
            sim.SetResult(AgilentE4406A.ChannelPowerRoot, new[] { -9.8, -78.0 });   // [total dBm, PSD dBm/Hz]
            provider.Add("GPIB0::18::INSTR", sim.Instrument);

            using (var session = provider.Open("GPIB0::18::INSTR"))
            {
                var reference = new TransmitterTesterPowerReference(
                    new AgilentE4406A(session), session, "Agilent E4406A VSA transmitter tester");

                Assert.Equal(ReferenceQuantity.RfPowerDbm, reference.Quantity);
                Assert.Equal("dBm", reference.Unit);

                reference.Prepare(new ReferencePoint { FrequencyMHz = 1000, NominalLevel = -10 });
                Assert.Equal(-9.8, reference.Measure(), 3);   // channel-power total (scalar[0]), not the PSD
            }
        }

        [Fact]
        public void Grades_an_e4438c_style_source_via_the_catalog_factory()
        {
            // The e4406a key resolves to the power-reference adapter and grades a source point.
            var provider = new SimulatedGpibProvider();
            var sim = new AgilentE4406ASimulatedDevice();
            sim.SetResult(AgilentE4406A.ChannelPowerRoot, new[] { -10.2 });   // ~ commanded -10 dBm
            provider.Add("GPIB0::18::INSTR", sim.Instrument);

            var choice = System.Linq.Enumerable.Single(
                GpibUtils.Verification.Catalog.VerificationCatalog.RfPowerReferences, r => r.Key == "e4406a");

            using (var session = provider.Open("GPIB0::18::INSTR"))
            {
                var reference = choice.Open(session);
                Assert.Equal(ReferenceQuantity.RfPowerDbm, reference.Quantity);
                reference.Prepare(new ReferencePoint { FrequencyMHz = 1000, NominalLevel = -10 });
                Assert.Equal(-10.2, reference.Measure(), 3);
                reference.Dispose();
            }
        }
    }
}
