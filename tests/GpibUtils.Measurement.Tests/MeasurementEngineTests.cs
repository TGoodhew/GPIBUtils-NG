using System;
using System.Linq;
using GpibUtils.Instruments.Meters;
using Xunit;

namespace GpibUtils.Measurement.Tests
{
    /// <summary>Exercises the ported attenuation-measurement engine (#34) against a deterministic fake bench,
    /// plus the 11793A LO/IF planner.</summary>
    public class MeasurementEngineTests
    {
        private static SweepOptions IdealOptions() => new SweepOptions
        {
            FreqStartMHz = 1000, FreqStopMHz = 1000, FreqStepMHz = 10,   // one (direct-regime) frequency
            AttenStartDb = 0, AttenStopDb = 30, AttenStepDb = 10,
            SettleMs = 0,
            AdaptiveLevel = false,
            RangeCalibrate = false,
            ForceRangeCal = false,
            FloorDetect = false,
            TrackMode = false
        };

        private static MeasurementEngine Engine(FakeBench bench, SweepOptions options) =>
            new MeasurementEngine(bench.Source, bench.Lo, bench.Attenuator, bench.Receiver, options);

        // ---- LO/IF planner (MicrowaveConverter) ----------------------------

        [Fact]
        public void Plan_below_crossover_is_direct()
        {
            var plan = MicrowaveConverter.Plan(1000, 2000, 26500);
            Assert.Equal(MeasurementRegime.Direct, plan.Regime);
            Assert.Equal(0, plan.LoMHz);
        }

        [Fact]
        public void Plan_above_crossover_uses_preferred_if()
        {
            var plan = MicrowaveConverter.Plan(5000, 2000, 26500);
            Assert.Equal(MeasurementRegime.Converted, plan.Regime);
            Assert.Equal(5000 + MicrowaveConverter.PreferredIfMHz, plan.LoMHz, 2);
            Assert.Equal(MicrowaveConverter.PreferredIfMHz, plan.IfMHz, 2);
            Assert.True(plan.LoAboveSignal);
        }

        [Fact]
        public void Plan_walks_the_if_ladder_when_lo_floor_blocks_preferred()
        {
            // RF just above crossover: RF+120.53 is below the 2 GHz LO floor, so the planner steps up the
            // IF ladder to the first IF whose LO clears the floor.
            var plan = MicrowaveConverter.Plan(1400, 2000, 26500);
            Assert.Equal(MeasurementRegime.Converted, plan.Regime);
            Assert.True(plan.LoMHz >= 2000);
            Assert.Contains(plan.IfMHz, MicrowaveConverter.IfLadderMHz.Concat(new[] { 2000.0 - 1400.0 }));
            Assert.NotNull(plan.Warning);
        }

        [Fact]
        public void Plan_throws_when_no_lo_if_fits()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MicrowaveConverter.Plan(50000, 2000, 26500));
        }

        // ---- sweep options -------------------------------------------------

        [Fact]
        public void Frequencies_enumerates_start_to_stop_inclusive()
        {
            var opts = new SweepOptions { FreqStartMHz = 1000, FreqStopMHz = 1030, FreqStepMHz = 10 };
            Assert.Equal(new[] { 1000.0, 1010.0, 1020.0, 1030.0 }, opts.Frequencies().ToArray());
        }

        // ---- engine end to end (fake bench) --------------------------------

        [Fact]
        public void Measure_frequency_recovers_the_commanded_attenuation()
        {
            var bench = new FakeBench();
            var engine = Engine(bench, IdealOptions());

            var result = engine.MeasureFrequency(1000);

            Assert.Equal(1000, result.FreqMHz);
            Assert.Equal(MeasurementRegime.Direct, result.Regime);
            Assert.Equal(4, result.Points.Count);                 // 0,10,20,30 dB
            // Ideal bench: each point recovers its commanded attenuation to ~zero error.
            Assert.True(result.MaxAbsErrorDb < 0.5, $"max error {result.MaxAbsErrorDb} dB");
            Assert.All(result.Points, p => Assert.Null(p.Error));
            var deepest = result.Points.Last();
            Assert.Equal(30, deepest.CommandedDb);
            Assert.Equal(30, deepest.MeasuredAttenuationDb, 1);
        }

        [Fact]
        public void Run_sweep_yields_one_result_per_frequency()
        {
            var bench = new FakeBench();
            var opts = IdealOptions();
            opts.FreqStopMHz = 1020;   // 1000, 1010, 1020 -> 3 frequencies
            var results = Engine(bench, opts).RunSweep().ToList();
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.Equal(4, r.Points.Count));
        }

        [Fact]
        public void Detect_signal_sees_the_source_when_rf_is_on()
        {
            var bench = new FakeBench();
            var det = Engine(bench, IdealOptions()).DetectSignal(1000);
            Assert.Equal(1000, det.FreqMHz);
            Assert.Equal(MeasurementRegime.Direct, det.Regime);
        }
    }
}
