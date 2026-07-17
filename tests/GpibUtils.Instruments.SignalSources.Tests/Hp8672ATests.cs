using System;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    /// <summary>Drives the <see cref="Hp8672A"/> driver against a simulated 8672A, including the post-retune
    /// phase-lock settle (the #96 <c>ExpectBitCleared</c> direct-bit, no-enable-mask path).</summary>
    public class Hp8672ATests
    {
        private static (Hp8672A driver, Hp8672ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp8672ASimulatedDevice();
            provider.Add(Hp8672A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp8672A.DefaultResource);
            var driver = new Hp8672A(session) { SettleTimeoutMs = 2000, PollIntervalMs = 5 };
            return (driver, sim, session);
        }

        [Fact]
        public void Is_a_signal_source()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ISignalSource>(driver);
        }

        [Fact]
        public void Default_address_is_factory_nineteen()
        {
            Assert.Equal("GPIB0::19::INSTR", Hp8672A.DefaultResource);
        }

        [Fact]
        public void Frequency_is_programmed_in_khz_with_execute()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(12596.365);
                Assert.Contains("P12596365Z", driver.History);
                Assert.Equal(12596.365, sim.FrequencyMHz, 3);
            }
        }

        [Fact]
        public void Frequency_pads_to_eight_digits()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(2000);        // 2 GHz => 02000000 kHz
                Assert.Contains("P02000000Z", driver.History);
                Assert.Equal(2000, sim.FrequencyMHz, 3);
            }
        }

        [Theory]
        [InlineData(-90, "K9L0")]
        [InlineData(-93, "K9L-3")]
        [InlineData(0, "K0L0")]
        [InlineData(3, "K0L3")]
        public void Power_decomposes_into_range_and_vernier(double dbm, string expected)
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetPowerDbm(dbm);
                Assert.Contains(expected, driver.History);
                Assert.Equal(dbm, sim.PowerDbm, 3);
            }
        }

        [Fact]
        public void Rf_on_off_toggles_output()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.RfOn();
                Assert.True(sim.RfOutputOn);
                driver.RfOff();
                Assert.False(sim.RfOutputOn);
            }
        }

        [Fact]
        public void Set_frequency_and_settle_waits_for_phase_lock()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyAndSettleMHz(10000);   // must not throw
                Assert.True(driver.IsPhaseLocked());
            }
        }

        [Fact]
        public void Settle_times_out_when_never_locks()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.RelockCompletes = false;
                driver.SettleTimeoutMs = 200;
                driver.SetFrequencyMHz(10000);
                Assert.Throws<Hp8672AException>(() => driver.WaitForPhaseLock());
            }
        }

        [Fact]
        public void Status_model_is_cleared_settle_without_enable_mask()
        {
            var model = Hp8672A.StatusModel();
            Assert.True(model.SrqSupported);
            Assert.Null(model.EnableMask);                            // no *SRE-equivalent to arm
            Assert.Null(model.RequestServiceBit);                    // => direct-bit flow
            Assert.Equal(8, model.BitValue("notPhaseLocked"));
            var op = model.Operations["phaseLock"];
            Assert.True(op.ExpectBitCleared);                        // #96: completes when the bit CLEARS
            Assert.Equal("notPhaseLocked", op.ExpectBit);
        }
    }
}
