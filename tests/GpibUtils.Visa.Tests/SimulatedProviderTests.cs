using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Visa.Tests
{
    public class SimulatedProviderTests
    {
        private static SimulatedGpibProvider NewProvider() => new SimulatedGpibProvider();

        [Fact]
        public void Idn_query_returns_identity()
        {
            var provider = NewProvider();
            provider.Add("GPIB0::14::INSTR", new SimulatedInstrument { IdentificationString = "ACME,DMM,1,2.0" });

            using (var s = provider.Open("GPIB0::14::INSTR"))
                Assert.Equal("ACME,DMM,1,2.0", s.Query("*IDN?"));
        }

        [Fact]
        public void Custom_responder_drives_replies()
        {
            var provider = NewProvider();
            provider.Add("GPIB0::7::INSTR", new SimulatedInstrument
            {
                Responder = cmd => cmd == "MEAS:VOLT?" ? "1.234E+0" : null
            });

            using (var s = provider.Open("GPIB0::7::INSTR"))
            {
                Assert.Equal("1.234E+0", s.Query("MEAS:VOLT?"));
                Assert.Equal("0", s.Query("SOMETHING:ELSE?")); // falls back to default
            }
        }

        [Fact]
        public void Serial_poll_reflects_status_and_srq()
        {
            var provider = NewProvider();
            var inst = provider.Add("GPIB0::5::INSTR");
            inst.StatusByte = 0x10;               // MAV
            inst.ServiceRequestPending = true;    // RQS

            using (var s = provider.Open("GPIB0::5::INSTR"))
            {
                var stb = s.SerialPoll();
                Assert.True(stb.MessageAvailable);
                Assert.True(stb.RequestingService);
                Assert.True(s.WaitForServiceRequest(100, out var elapsed));
                Assert.Equal(0, elapsed);
            }
        }

        [Fact]
        public void Cls_clears_status()
        {
            var provider = NewProvider();
            var inst = provider.Add("GPIB0::9::INSTR");
            inst.StatusByte = 0x20;
            inst.ServiceRequestPending = true;

            using (var s = provider.Open("GPIB0::9::INSTR"))
            {
                s.Write("*CLS");
                Assert.Equal(0, s.SerialPoll().Value);
                Assert.False(s.WaitForServiceRequest(10, out _));
            }
        }

        [Fact]
        public void Auto_create_can_be_disabled()
        {
            var provider = NewProvider();
            provider.AutoCreate = false;
            Assert.Throws<GpibException>(() => provider.Open("GPIB0::22::INSTR"));
        }

        [Fact]
        public void Open_through_registry_by_name()
        {
            using (var s = GpibProviders.Open("Simulated", "GPIB0::1::INSTR", null))
                Assert.Equal("GPIBUtils,Simulated Instrument,0,1.0", s.Query("*IDN?"));
        }
    }
}
