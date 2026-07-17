using GpibUtils.Instruments.PowerSupplies;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.PowerSupplies.Tests
{
    public class Hp6625ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp6625A.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "sim",
                Responder = c =>
                {
                    var t = c.Trim();
                    if (t == "ID?") return "Agilent 6625A";
                    if (t.StartsWith("VOUT?")) return "5.001";
                    if (t.StartsWith("IOUT?")) return "0.250";
                    return null;
                }
            });
            return provider.Open(Hp6625A.DefaultResource);
        }

        [Fact]
        public void Implements_supply_and_default_address()
        {
            using (var s = Open())
            {
                var d = new Hp6625A(s);
                Assert.IsAssignableFrom<IDcPowerSupply>(d);
                Assert.Equal("GPIB0::5::INSTR", Hp6625A.DefaultResource);
                Assert.Contains("6625A", d.Identify());
            }
        }

        [Fact]
        public void Channel_scoped_set_and_measure()
        {
            using (var s = Open())
            {
                var d = new Hp6625A(s);
                d.SetVoltage(5); d.SetCurrentLimit(0.5); d.SetOutput(true);
                Assert.Contains("VSET 1,5", d.History);
                Assert.Contains("ISET 1,0.5", d.History);
                Assert.Contains("OUT 1,1", d.History);
                Assert.Equal(5.001, d.MeasureVoltage(), 3);
                Assert.Equal(0.250, d.MeasureCurrent(), 3);

                d.SelectedChannel = 2;
                d.SetVoltage(12);
                Assert.Contains("VSET 2,12", d.History);
            }
        }
    }
}
