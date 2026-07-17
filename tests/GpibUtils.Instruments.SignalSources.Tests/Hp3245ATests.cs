using System;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    public class Hp3245ATests
    {
        private static IInstrumentSession Open()
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(Hp3245A.DefaultResource, new SimulatedInstrument
            {
                IdentificationString = "HP3245",
                Responder = c =>
                {
                    if (c.Contains("ID?")) return "HP3245";
                    if (c.Contains("OUTPUT?")) return "+3.500000E+00";
                    return null;
                }
            });
            return provider.Open(Hp3245A.DefaultResource);
        }

        [Fact]
        public void Implements_universal_source_and_default_address_is_9()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                Assert.IsAssignableFrom<IUniversalSource>(d);
                Assert.Equal("GPIB0::9::INSTR", Hp3245A.DefaultResource);
                Assert.Equal("HP3245", d.Identify());
            }
        }

        [Fact]
        public void Select_channel_uses_use_codes()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                d.SelectChannel(UniversalSourceChannel.ChannelA);
                d.SelectChannel(UniversalSourceChannel.ChannelB);
                Assert.Equal(new[] { "USE 0", "USE 100" }, d.History);
            }
        }

        [Fact]
        public void Set_dc_voltage_and_current()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                d.SetDcVoltage(3.5);
                d.SetDcCurrent(0.05);
                Assert.Equal(new[] { "APPLY DCV 3.5", "APPLY DCI 0.05" }, d.History);
            }
        }

        [Fact]
        public void Out_of_range_outputs_throw()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                Assert.Throws<ArgumentOutOfRangeException>(() => d.SetDcVoltage(11));
                Assert.Throws<ArgumentOutOfRangeException>(() => d.SetDcCurrent(0.2));
            }
        }

        [Fact]
        public void Autorange_and_read_output()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                d.SetAutorange(true);
                Assert.Contains("ARANGE ON", d.History);
                Assert.Equal(3.5, d.ReadOutput(), 3);
            }
        }

        [Fact]
        public void Request_service_mask_uses_rqs()
        {
            using (var s = Open())
            {
                var d = new Hp3245A(s);
                d.SetRequestServiceMask(48);   // READY | ERROR
                Assert.Equal("RQS 48", Assert.Single(d.History));
            }
        }
    }
}
