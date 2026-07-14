using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Meters.Tests
{
    /// <summary>
    /// Drives the <see cref="Hp34401A"/> driver against a simulated 34401A over the standard transport, so
    /// assertions exercise the real write / query / parse path with no hardware.
    /// </summary>
    public class Hp34401ATests
    {
        private static (Hp34401A driver, Hp34401ASimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new Hp34401ASimulatedDevice();
            provider.Add(Hp34401A.DefaultResource, sim.Instrument);
            var session = provider.Open(Hp34401A.DefaultResource);
            return (new Hp34401A(session), sim, session);
        }

        [Fact]
        public void Default_resource_is_factory_gpib_22()
        {
            Assert.Equal("GPIB0::22::INSTR", Hp34401A.DefaultResource);
        }

        [Fact]
        public void Is_a_digital_multimeter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IDigitalMultimeter>(driver);
        }

        [Fact]
        public void Initialize_clears_then_resets_then_clears_status()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Equal<string>(new[] { "*RST", "*CLS" }, driver.History);
                Assert.True(sim.WasReset);
            }
        }

        [Fact]
        public void Identify_returns_the_idn_string()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("34401A", driver.Identify());
        }

        // ---- configuration ------------------------------------------------------

        [Fact]
        public void Configure_dc_voltage_with_range_and_resolution()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Configure(MeasurementFunction.DcVoltage, "10", "0.001");
                Assert.Equal("CONF:VOLT:DC 10,0.001", Assert.Single(driver.History));
                Assert.Equal("VOLT:DC", sim.Function);
                Assert.Equal("10,0.001", sim.ConfiguredRange);
                Assert.False(sim.WasReset);
            }
        }

        [Fact]
        public void Configure_resolution_only_uses_DEF_range()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Configure(MeasurementFunction.DcVoltage, null, "0.0001");
                Assert.Equal("CONF:VOLT:DC DEF,0.0001", Assert.Single(driver.History));
            }
        }

        [Fact]
        public void Configure_bare_function_has_no_argument()
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Configure(MeasurementFunction.DcVoltage);
                Assert.Equal("CONF:VOLT:DC", Assert.Single(driver.History));
            }
        }

        [Theory]
        [InlineData(MeasurementFunction.Continuity, "CONF:CONT")]
        [InlineData(MeasurementFunction.Diode, "CONF:DIOD")]
        public void Configure_continuity_and_diode_take_no_range(MeasurementFunction fn, string expected)
        {
            var (driver, _, session) = Bench();
            using (session)
            {
                driver.Configure(fn, "10", "0.1");   // range/res ignored for these
                Assert.Equal(expected, Assert.Single(driver.History));
            }
        }

        [Fact]
        public void Configure_four_wire_resistance_uses_FRES_root()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Configure(MeasurementFunction.Resistance4Wire);
                Assert.Equal("CONF:FRES", Assert.Single(driver.History));
                Assert.Equal("FRES", sim.Function);
            }
        }

        [Fact]
        public void SetNplc_targets_the_function_root()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetNplc(MeasurementFunction.DcVoltage, 10);
                Assert.Equal("VOLT:DC:NPLC 10", Assert.Single(driver.History));
                Assert.Equal(10, sim.Nplc);
            }
        }

        [Fact]
        public void SetNplc_on_continuity_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentException>(() => driver.SetNplc(MeasurementFunction.Continuity, 1));
        }

        [Fact]
        public void SetAutoRange_and_input_impedance_and_autozero()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetAutoRange(MeasurementFunction.DcVoltage, false);
                driver.SetInputImpedanceAuto(true);
                driver.SetAutoZero(AutoZeroMode.Once);
                Assert.Contains("VOLT:DC:RANG:AUTO OFF", driver.History);
                Assert.True(sim.InputImpedanceAuto);
                Assert.Equal("ONCE", sim.AutoZero);
            }
        }

        // ---- trigger / sample ---------------------------------------------------

        [Fact]
        public void Trigger_and_sample_settings_are_sent_and_decoded()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetTriggerSource(TriggerSource.Bus);
                driver.SetTriggerCount(5);
                driver.SetSampleCount(100);
                driver.SetTriggerDelay(null);
                Assert.Contains("TRIG:SOUR BUS", driver.History);
                Assert.Contains("TRIG:COUN 5", driver.History);
                Assert.Contains("SAMP:COUN 100", driver.History);
                Assert.Contains("TRIG:DEL:AUTO ON", driver.History);
                Assert.Equal("BUS", sim.TriggerSource);
                Assert.Equal(5, sim.TriggerCount);
                Assert.Equal(100, sim.SampleCount);
            }
        }

        [Fact]
        public void SetTriggerCount_rejects_below_one()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SetTriggerCount(0));
        }

        // ---- reads --------------------------------------------------------------

        [Fact]
        public void ReadValue_parses_a_single_reading()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = 1.04530e-3;
                Assert.Equal(1.04530e-3, driver.ReadValue(), 9);
            }
        }

        [Fact]
        public void ReadValues_sets_sample_count_and_returns_the_burst()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Readings = new[] { -1.0, 0.0, 1.0, 2.5 };
                var burst = driver.ReadValues(4);
                Assert.Equal(new[] { -1.0, 0.0, 1.0, 2.5 }, burst);
                Assert.Contains("SAMP:COUN 4", driver.History);
                Assert.Equal(4, sim.SampleCount);
            }
        }

        [Fact]
        public void ReadValues_default_burst_repeats_the_reading()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.Reading = 5.0;
                var burst = driver.ReadValues(3);
                Assert.Equal(new[] { 5.0, 5.0, 5.0 }, burst);
            }
        }

        [Fact]
        public void SelfTest_reflects_the_tst_reply()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                Assert.True(driver.SelfTest());
                sim.SelfTestPasses = false;
                Assert.False(driver.SelfTest());
            }
        }

        // ---- math ---------------------------------------------------------------

        [Fact]
        public void Math_null_offset_sequence()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetMathFunction(Hp34401A.MathFunction.Null);
                driver.EnableMath(true);
                driver.SetNullOffset(0.005);
                Assert.Contains("CALC:FUNC NULL", driver.History);
                Assert.Contains("CALC:STAT ON", driver.History);
                Assert.Contains("CALC:NULL:OFFS 0.005", driver.History);
                Assert.Equal("NULL", sim.MathFunction);
                Assert.True(sim.MathEnabled);
            }
        }

        [Fact]
        public void ReadAverageStatistics_reads_the_calc_average_registers()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.AverageMin = -0.1; sim.AverageMax = 0.3; sim.AverageMean = 0.1; sim.AverageCount = 42;
                var (min, max, avg, count) = driver.ReadAverageStatistics();
                Assert.Equal(-0.1, min, 6);
                Assert.Equal(0.3, max, 6);
                Assert.Equal(0.1, avg, 6);
                Assert.Equal(42, count);
            }
        }

        // ---- display ------------------------------------------------------------

        [Fact]
        public void Display_text_is_truncated_to_twelve_chars_and_quotes_stripped()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetDisplayText("Zero \"CW\" position");   // > 12 chars, embedded quotes
                Assert.Equal("DISP:TEXT \"Zero CW posi\"", Assert.Single(driver.History));
                Assert.Equal("Zero CW posi", sim.DisplayText);
                driver.ClearDisplayText();
                Assert.Null(sim.DisplayText);
            }
        }

        // ---- status / errors ----------------------------------------------------

        [Fact]
        public void NextError_is_no_error_when_queue_empty()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Equal(Hp34401A.NoError, driver.NextError());
        }

        [Fact]
        public void DrainErrors_returns_queued_errors_then_stops_at_no_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.QueueError("-113,\"Undefined header\"");
                sim.QueueError("-410,\"Query INTERRUPTED\"");
                var errors = driver.DrainErrors();
                Assert.Equal(2, errors.Count);
                Assert.Equal("-113,\"Undefined header\"", errors[0]);
            }
        }

        // ---- parsing ------------------------------------------------------------

        [Theory]
        [InlineData("+1.04530000E-03", 1.04530e-3)]
        [InlineData("-4.25000000E+01", -42.5)]
        [InlineData("0", 0.0)]
        public void ParseReading_parses_scientific_notation(string raw, double expected)
        {
            Assert.Equal(expected, Hp34401A.ParseReading(raw), 9);
        }

        [Fact]
        public void ParseReading_rejects_non_numeric()
        {
            Assert.Throws<FormatException>(() => Hp34401A.ParseReading("N/A"));
        }

        [Fact]
        public void ParseReadingList_splits_comma_separated_burst()
        {
            var v = Hp34401A.ParseReadingList("+1.0E+00,+2.0E+00,+3.0E+00");
            Assert.Equal(new[] { 1.0, 2.0, 3.0 }, v);
        }

        // ---- statistics ---------------------------------------------------------

        [Fact]
        public void DmmStatistics_computes_min_max_mean_stddev()
        {
            var s = DmmStatistics.Of(new[] { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 });
            Assert.Equal(2.0, s.Min, 9);
            Assert.Equal(9.0, s.Max, 9);
            Assert.Equal(5.0, s.Average, 9);
            Assert.Equal(2.138089935, s.StdDev, 6);   // sample (n-1) standard deviation
            Assert.Equal(8, s.Count);
        }

        [Fact]
        public void DmmStatistics_single_value_has_zero_stddev()
        {
            var s = DmmStatistics.Of(new[] { 3.3 });
            Assert.Equal(0.0, s.StdDev, 9);
            Assert.Equal(3.3, s.Average, 9);
        }

        [Fact]
        public void DmmStatistics_empty_throws()
        {
            Assert.Throws<ArgumentException>(() => DmmStatistics.Of(Array.Empty<double>()));
        }
    }
}
