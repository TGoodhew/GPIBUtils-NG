using System.Linq;
using GpibUtils.Console.Instruments;
using GpibUtils.Instruments.Meters;
using GpibUtils.Verification;
using GpibUtils.Verification.Catalog;
using GpibUtils.Verification.References;
using GpibUtils.Visa;
using GpibUtils.Visa.Providers;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Console.Tests
{
    /// <summary>
    /// Covers the simulated-bench coupling that makes <c>verify harness</c> / <c>verify source</c> runnable
    /// with no hardware: seeding the right model at each reference's resource before it is opened, deduping
    /// one instrument filling two roles, and reporting references that have no simulated model.
    /// </summary>
    public class SimulatedHarnessBenchTests
    {
        private static ReferenceChoice Power(string key) =>
            VerificationCatalog.RfPowerReferences.Single(r => r.Key == key);

        private static ReferenceChoice Freq(string key) =>
            VerificationCatalog.FrequencyReferences.Single(r => r.Key == key);

        private static ReferenceChoice Volts(string key) =>
            VerificationCatalog.DcVoltageReferences.Single(r => r.Key == key);

        [Fact]
        public void Returns_null_for_a_non_simulated_provider()
        {
            var real = new Ar488GpibProvider();
            var coupling = SimulatedHarnessBench.TrySeed(real,
                new[] { new SimReferenceRequest("GPIB0::14::INSTR", Power("hp8902a")) });

            Assert.Null(coupling);
        }

        [Fact]
        public void Seeds_a_model_at_the_reference_resource_so_it_is_not_auto_created_generic()
        {
            var sim = new SimulatedGpibProvider();
            var coupling = SimulatedHarnessBench.TrySeed(sim,
                new[] { new SimReferenceRequest("GPIB0::13::INSTR", Power("e4418b")) });

            Assert.NotNull(coupling);
            Assert.True(coupling.AnySeeded);
            Assert.Empty(coupling.Warnings);
            // A seeded resource is registered up front; the generic auto-create path would leave it absent.
            Assert.Contains("GPIB0::13::INSTR", sim.Discover());
        }

        [Fact]
        public void Power_meter_reads_back_the_commanded_level()
        {
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::13::INSTR";
            var choice = Power("e4418b");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(res, choice) });

            coupling.Bench.CarrierPowerDbm = -12.5;
            coupling.Apply();

            using (var reference = choice.Open(sim.Open(res)))
            {
                reference.Prepare(new ReferencePoint { FrequencyMHz = 1000 });
                Assert.Equal(-12.5, reference.Measure(), 6);
            }
        }

        [Fact]
        public void Counter_reads_back_the_commanded_frequency()
        {
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::3::INSTR";
            var choice = Freq("hp53131a");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(res, choice) });

            coupling.Bench.CarrierFrequencyHz = 1.2345e9;
            coupling.Apply();

            using (var reference = choice.Open(sim.Open(res)))
            {
                reference.Prepare(new ReferencePoint { FrequencyMHz = 1234.5 });
                Assert.Equal(1.2345e9, reference.Measure(), 0);
            }
        }

        [Fact]
        public void Spectrum_analyzer_level_survives_the_peak_marker_recompute()
        {
            // The simulator recomputes MarkerAmplitudeDbm from the peak of Trace when the driver sends
            // MKPK HI, so the level has to be seeded via Trace — seeding the marker alone would be lost.
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::18::INSTR";
            var choice = Power("hp8560e");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(res, choice) });

            coupling.Bench.CarrierPowerDbm = -33.25;
            coupling.Apply();

            using (var reference = choice.Open(sim.Open(res)))
            {
                reference.Prepare(new ReferencePoint { FrequencyMHz = 1000 });
                Assert.Equal(-33.25, reference.Measure(), 6);
            }
        }

        [Fact]
        public void One_receiver_can_fill_both_the_power_and_frequency_roles()
        {
            // The 8902A answers every read from a single Reading whose units depend on the selected mode,
            // so this only works if the value is resolved at read time rather than at set-point time.
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::14::INSTR";
            var powerChoice = Power("hp8902a");
            var freqChoice = Freq("hp8902a");

            var coupling = SimulatedHarnessBench.TrySeed(sim, new[]
            {
                new SimReferenceRequest(res, powerChoice),
                new SimReferenceRequest(res, freqChoice),
            });

            Assert.NotNull(coupling);
            Assert.Empty(coupling.Warnings);          // self-syncing model must not read as "unsupported"

            coupling.Bench.CarrierPowerDbm = -7.5;
            coupling.Bench.CarrierFrequencyHz = 2.5e9;
            coupling.Apply();

            using (var powerRef = powerChoice.Open(sim.Open(res)))
            {
                powerRef.Prepare(new ReferencePoint { FrequencyMHz = 2500 });
                Assert.Equal(-7.5, powerRef.Measure(), 6);
            }
            using (var freqRef = freqChoice.Open(sim.Open(res)))
            {
                freqRef.Prepare(new ReferencePoint { FrequencyMHz = 2500 });
                Assert.Equal(2.5e9, freqRef.Measure(), 0);
            }
        }

        [Theory]
        [InlineData(-20.0)]
        [InlineData(-40.0)]
        [InlineData(-90.0)]
        public void Receiver_power_stays_accurate_at_low_levels(double dbm)
        {
            // Guards a real defect: the model renders Reading as watts with "0.######", which rounds
            // anything below roughly -30 dBm to zero and yields NaN dBm.
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::14::INSTR";
            var choice = Power("hp8902a");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(res, choice) });

            coupling.Bench.CarrierPowerDbm = dbm;
            coupling.Apply();

            using (var reference = choice.Open(sim.Open(res)))
            {
                reference.Prepare(new ReferencePoint { FrequencyMHz = 1000 });
                Assert.Equal(dbm, reference.Measure(), 6);
            }
        }

        [Fact]
        public void Dmm_reads_back_the_commanded_volts()
        {
            var sim = new SimulatedGpibProvider();
            const string res = "GPIB0::22::INSTR";
            var choice = Volts("hp34401a");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(res, choice) });

            coupling.Bench.SourceVolts = 10.0;
            coupling.Apply();

            using (var reference = choice.Open(sim.Open(res)))
            {
                reference.Prepare(new ReferencePoint { NominalLevel = 10.0 });
                Assert.Equal(10.0, reference.Measure(), 6);
            }
        }

        [Fact]
        public void A_reference_with_no_simulated_model_warns_instead_of_failing_silently()
        {
            var sim = new SimulatedGpibProvider();
            var coupling = SimulatedHarnessBench.TrySeed(sim,
                new[] { new SimReferenceRequest("GPIB0::13::INSTR", Power("hp437b")) });

            // The coupling is still returned so the warning reaches the user, but nothing was seeded.
            Assert.NotNull(coupling);
            Assert.False(coupling.AnySeeded);
            Assert.Contains(coupling.Warnings, w => w.Contains("hp437b"));
        }

        [Fact]
        public void Two_different_instruments_at_one_address_warn_and_only_the_first_is_simulated()
        {
            var sim = new SimulatedGpibProvider();
            const string clash = "GPIB0::18::INSTR";
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[]
            {
                new SimReferenceRequest(clash, Power("hp8560e")),
                new SimReferenceRequest(clash, Freq("hp53131a")),
            });

            Assert.True(coupling.AnySeeded);
            Assert.Contains(coupling.Warnings, w => w.Contains("hp53131a") && w.Contains("only the first"));
        }

        [Fact]
        public void Coupled_source_pushes_commanded_setpoints_into_the_references()
        {
            var sim = new SimulatedGpibProvider();
            const string meterRes = "GPIB0::13::INSTR";
            var choice = Power("e4418b");
            var coupling = SimulatedHarnessBench.TrySeed(sim, new[] { new SimReferenceRequest(meterRes, choice) });

            var inner = new RecordingSource();
            var coupled = coupling.Couple(inner);

            coupled.SetFrequencyMHz(2000);
            coupled.SetPowerDbm(-4.25);

            // Forwarded to the real driver...
            Assert.Equal(2000, inner.LastFrequencyMHz);
            Assert.Equal(-4.25, inner.LastPowerDbm);
            // ...and mirrored onto the bench, in Hz.
            Assert.Equal(2.0e9, coupling.Bench.CarrierFrequencyHz);

            using (var reference = choice.Open(sim.Open(meterRes)))
            {
                reference.Prepare(new ReferencePoint { FrequencyMHz = 2000 });
                Assert.Equal(-4.25, reference.Measure(), 6);
            }
        }

        private sealed class RecordingSource : GpibUtils.Instruments.SignalSources.ISignalSource
        {
            public double LastFrequencyMHz { get; private set; }
            public double LastPowerDbm { get; private set; }

            public string ResourceName => "FAKE";
            public void Initialize() { }
            public void Preset() { }
            public void RfOn() { }
            public void RfOff() { }
            public void SetFrequencyMHz(double mhz) => LastFrequencyMHz = mhz;
            public void SetPowerDbm(double dbm) => LastPowerDbm = dbm;
        }
    }
}
