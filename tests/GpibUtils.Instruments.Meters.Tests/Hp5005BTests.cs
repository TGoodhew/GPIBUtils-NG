using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>Drives the <see cref="Hp5005B"/> driver against a simulated 5005B, including the vendor
    /// <c>QM</c>-mask / data-ready SRQ measurement handshake (a custom non-<c>*SRE</c> enable command + custom
    /// bit table through the shared #43/#96 engine).</summary>
    public class Hp5005BTests
    {
        private static (Hp5005B driver, Hp5005BSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp5005BSimulatedDevice();
            provider.Add(Hp5005B.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp5005B.DefaultResource);
            var driver = new Hp5005B(session) { MeasureTimeoutMs = 2000, PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_a_signature_analyzer()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ISignatureAnalyzer>(driver);
        }

        [Fact]
        public void Default_address_is_factory_three()
        {
            Assert.Equal("GPIB0::3::INSTR", Hp5005B.DefaultResource);
        }

        [Fact]
        public void Identify_uses_id_command()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("5005B", driver.Identify());
        }

        [Fact]
        public void Initialize_resets_to_defaults()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("RS", sim.Commands);
            }
        }

        [Fact]
        public void Function_select_sends_fn_code()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFunction(SignatureFunction.Resistance);
                Assert.Contains("F5", sim.Commands);
                Assert.Equal(5, sim.Function);
            }
        }

        [Fact]
        public void Measure_numeric_arms_qm_mask_and_returns_value()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MeasurementValue = 3.3;
                double v = driver.Measure(SignatureFunction.DcVoltage);
                Assert.Equal(3.3, v, 3);
                Assert.Contains("QM5", sim.Commands);   // mask = data-ready(1) | error(4)
            }
        }

        [Fact]
        public void Trigger_and_read_returns_signature_for_sa_functions()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Signature = "C0DE";
                driver.SetFunction(SignatureFunction.NormSignature);
                Assert.Equal("C0DE", driver.TriggerAndRead());
            }
        }

        [Fact]
        public void Measure_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorOnMeasure = true;
                var ex = Assert.Throws<Hp5005BException>(() => driver.Measure(SignatureFunction.Resistance));
                Assert.False(ex.IsTimeout);
            }
        }

        [Fact]
        public void Measure_times_out_when_never_completes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MeasurementCompletes = false;
                driver.MeasureTimeoutMs = 200;
                var ex = Assert.Throws<Hp5005BException>(() => driver.Measure(SignatureFunction.Resistance));
                Assert.True(ex.IsTimeout);
            }
        }

        [Fact]
        public void Read_error_code_uses_se_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ErrorCode = 7;
                Assert.Equal(7, driver.ReadErrorCode());
            }
        }

        [Fact]
        public void Status_model_uses_qm_enable_and_data_ready()
        {
            var model = Hp5005B.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Equal("QM{mask}", model.EnableMask.SetCommand);   // custom (non-*SRE) enable command
            Assert.Equal("QM0", model.EnableMask.ClearCommand);
            Assert.Equal("srqFlag", model.RequestServiceBit);
            Assert.Equal(1, model.BitValue("dataReady"));
            Assert.Equal(64, model.BitValue("srqFlag"));
            Assert.Equal("dataReady", model.Operations["measurement"].ExpectBit);
        }
    }
}
