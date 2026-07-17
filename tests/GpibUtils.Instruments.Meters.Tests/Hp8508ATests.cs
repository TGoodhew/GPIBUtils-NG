using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    public class Hp8508ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp8508A.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HEWLETT-PACKARD,8508A-050,0,REV 2944",
                Responder = c =>
                {
                    var t = c.Trim();
                    if (t == "MEASure? TRANsmission") return "-3.50";
                    if (t == "MEASure? AVOLtage,BVOLtage,PHASe") return "1.000E+0,5.000E-1,4.500E+1";
                    return null;
                }
            });
            return provider.Open(Hp8508A.DefaultResource);
        }

        [Fact]
        public void Implements_vector_voltmeter_and_identifies()
        {
            using (var s = Open())
            {
                var d = new Hp8508A(s);
                Assert.IsAssignableFrom<IVectorVoltmeter>(d);
                Assert.Contains("8508A", d.Identify());
            }
        }

        [Fact]
        public void Initialize_enables_auto_band()
        {
            using (var s = Open())
            {
                var d = new Hp8508A(s);
                d.Initialize();
                Assert.Contains("FREQuency:BAND:AUTO ON", d.History);
            }
        }

        [Fact]
        public void Averaging_count_is_range_checked()
        {
            using (var s = Open())
            {
                var d = new Hp8508A(s);
                d.SetAveragingCount(5);
                Assert.Contains("AVERage:COUNt 5", d.History);
                Assert.Throws<ArgumentOutOfRangeException>(() => d.SetAveragingCount(11));
            }
        }

        [Fact]
        public void Measure_single_quantity()
        {
            using (var s = Open())
            {
                var d = new Hp8508A(s);
                Assert.Equal(-3.5, d.Measure(VectorMeasurement.Transmission), 3);
                Assert.Contains("MEASure? TRANsmission", d.History);
            }
        }

        [Fact]
        public void Measure_many_returns_one_value_each()
        {
            using (var s = Open())
            {
                var d = new Hp8508A(s);
                var r = d.MeasureMany(VectorMeasurement.ChannelAVoltage, VectorMeasurement.ChannelBVoltage, VectorMeasurement.Phase);
                Assert.Equal(3, r.Count);
                Assert.Equal(1.0, r[0], 3);
                Assert.Equal(0.5, r[1], 3);
                Assert.Equal(45.0, r[2], 3);
                Assert.Contains("MEASure? AVOLtage,BVOLtage,PHASe", d.History);
            }
        }
    }
}
