using System;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Calibrators.Tests
{
    /// <summary>Drives the <see cref="Fluke5440A"/> driver against a simulated 5440 over the standard transport.</summary>
    public class Fluke5440ATests
    {
        private static (Fluke5440A driver, Fluke5440ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Fluke5440ASimulatedDevice();
            provider.Add(Fluke5440A.DefaultResource, sim.Instrument);
            var session = provider.Open(Fluke5440A.DefaultResource);
            return (new Fluke5440A(session), sim, session);
        }

        [Fact]
        public void Is_a_dc_voltage_calibrator()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IDcVoltageCalibrator>(driver);
        }

        [Fact]
        public void Default_address_is_factory_seven()
        {
            Assert.Equal("GPIB0::7::INSTR", Fluke5440A.DefaultResource);
        }

        [Fact]
        public void Identify_has_no_idn_but_names_the_model()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("5440", driver.Identify());
        }

        [Fact]
        public void Firmware_version_uses_gvrs()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.FirmwareVersion = "02.03";
                Assert.Equal("02.03", driver.FirmwareVersion());
                Assert.Contains("GVRS", driver.History);
            }
        }

        [Fact]
        public void Initialize_clears_and_resets()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOutputVolts(10);
                driver.Initialize();
                Assert.Contains("RESET", driver.History);
                Assert.Equal(0, sim.OutputVolts);
                Assert.False(sim.IsOperating);
            }
        }

        [Fact]
        public void Set_and_get_output_round_trips()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOutputVolts(12.34567);
                Assert.Equal("SOUT 12.34567", Assert.Single(driver.History));
                Assert.Equal(12.34567, sim.OutputVolts, 5);
                Assert.Equal(12.34567, driver.GetOutputVolts(), 5);
            }
        }

        [Fact]
        public void Output_format_stays_under_eight_significant_digits()
        {
            // 5440 manual: < 8 significant digits. G7 keeps us inside that bound.
            Assert.Equal("1.234568", Fluke5440A.Format(1.23456789));
            Assert.Equal("-1100", Fluke5440A.Format(-1100.0));
        }

        [Fact]
        public void Operate_and_standby()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Operate();
                Assert.True(sim.IsOperating);
                Assert.Contains("OPER", driver.History);
                driver.Standby();
                Assert.False(sim.IsOperating);
                Assert.Contains("STBY", driver.History);
            }
        }

        [Fact]
        public void Set_output_state_enum_maps_to_mnemonics()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOutputState(CalibratorOutputState.Operate);
                Assert.True(sim.IsOperating);
                driver.SetOutputState(CalibratorOutputState.Standby);
                Assert.False(sim.IsOperating);
            }
        }

        [Fact]
        public void Sense_mode_selects_esns_isns()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetSenseMode(CalibratorSenseMode.ExternalFourWire);
                Assert.True(sim.ExternalSense);
                Assert.Contains("ESNS", driver.History);
                driver.SetSenseMode(CalibratorSenseMode.InternalTwoWire);
                Assert.False(sim.ExternalSense);
                Assert.Contains("ISNS", driver.History);
            }
        }

        [Fact]
        public void Increment_adds_to_output()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOutputVolts(10);
                driver.IncrementOutput(0.5);
                Assert.Equal(10.5, sim.OutputVolts, 5);
                Assert.Contains("INCR 0.5", driver.History);
            }
        }

        [Fact]
        public void Reference_store_and_recall()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetOutputVolts(7.5);
                driver.StoreReference();
                driver.SetOutputVolts(1.0);
                driver.GoToReference();
                Assert.Equal(7.5, sim.OutputVolts, 5);
            }
        }

        [Fact]
        public void Boost_mode_maps_to_mnemonics()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetBoostMode(Fluke5440BoostMode.Voltage);
                Assert.Contains("BSTV", driver.History);
                driver.SetBoostMode(Fluke5440BoostMode.Current);
                Assert.Contains("BSTC", driver.History);
                driver.SetBoostMode(Fluke5440BoostMode.Off);
                Assert.Contains("BSTO", driver.History);
            }
        }

        [Fact]
        public void Guard_and_divider()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetExternalGuard(true);
                Assert.Contains("EGRD", driver.History);
                driver.SetExternalGuard(false);
                Assert.Contains("IGRD", driver.History);
                driver.SetDivider(true);
                Assert.Contains("DIVY", driver.History);
                driver.SetDivider(false);
                Assert.Contains("DIVN", driver.History);
            }
        }

        [Fact]
        public void Limits_round_trip()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetVoltageLimit(100);
                Assert.Contains("SVLM 100", driver.History);
                Assert.Equal(100, Fluke5440A.ParseValue(driver.GetVoltageLimits(), "GVLM"), 5);
                driver.SetCurrentLimit(0.01);
                Assert.Contains("SCLM 0.01", driver.History);
                Assert.Equal(0.01, Fluke5440A.ParseValue(driver.GetCurrentLimits(), "GCLM"), 5);
            }
        }

        [Fact]
        public void Srq_mask_round_trips()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SetSrqMask(40);
                Assert.Contains("SSRQ 40", driver.History);
                Assert.Equal(40, driver.GetSrqMask());
            }
        }

        [Fact]
        public void Error_query_reads_and_clears()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = 5;
                Assert.Equal(5, driver.GetError());
                Assert.Equal(0, driver.GetError());   // cleared after read
            }
        }

        [Fact]
        public void Doing_state_reports_idle_by_default()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                Assert.Equal(0, driver.GetDoingState());
                sim.DoingState = 3;
                Assert.Equal(3, driver.GetDoingState());
            }
        }

        [Fact]
        public void Self_tests_send_mnemonics()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.SelfTestAnalog();
                driver.SelfTestDigital();
                driver.SelfTestHighVoltage();
                Assert.Contains("TSTA", driver.History);
                Assert.Contains("TSTD", driver.History);
                Assert.Contains("TSTH", driver.History);
            }
        }

        [Fact]
        public void Get_output_on_empty_response_throws()
        {
            Assert.Throws<FormatException>(() => Fluke5440A.ParseValue("", "GOUT"));
        }
    }
}
