using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Instruments.Counters;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification;
using GpibUtils.Verification.Catalog;
using GpibUtils.Verification.References;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// The shared "what the DUT is currently emitting" state of a simulated bench. The DUT decorators write
    /// to it as the verifier commands set-points; the seeded reference models read from it.
    /// </summary>
    internal sealed class SimulatedBench
    {
        public double CarrierFrequencyHz;
        public double CarrierPowerDbm;
        public double SourceVolts;
    }

    /// <summary>One reference instrument the harness is about to open, at its already-resolved resource.</summary>
    internal sealed class SimReferenceRequest
    {
        public SimReferenceRequest(string resource, ReferenceChoice choice)
        {
            Resource = resource;
            Choice = choice;
        }

        public string Resource { get; }
        public ReferenceChoice Choice { get; }
    }

    /// <summary>
    /// A seeded simulator model plus the per-role hooks that push the bench state into it. A role hook is
    /// null when that model cannot fill the role (e.g. a counter has no power reading).
    /// </summary>
    internal sealed class SimReferenceModel
    {
        public SimReferenceModel(SimulatedInstrument instrument)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
        }

        public SimulatedInstrument Instrument { get; }

        public Action<SimulatedBench> PowerSync { get; set; }
        public Action<SimulatedBench> FreqSync { get; set; }
        public Action<SimulatedBench> VoltSync { get; set; }

        /// <summary>
        /// Roles this model serves without a set-point hook because it reads the bench directly at
        /// measurement time (the 8902A, whose reading units depend on the mode selected at read time). These
        /// are supported, not missing — they must not be reported as unsupported.
        /// </summary>
        public ISet<ReferenceQuantity> SelfSyncedQuantities { get; } = new HashSet<ReferenceQuantity>();

        /// <summary>True when the model covers <paramref name="quantity"/> by hook or by self-sync.</summary>
        public bool Supports(ReferenceQuantity quantity) =>
            SyncFor(quantity) != null || SelfSyncedQuantities.Contains(quantity);

        public Action<SimulatedBench> SyncFor(ReferenceQuantity quantity)
        {
            switch (quantity)
            {
                case ReferenceQuantity.RfPowerDbm: return PowerSync;
                case ReferenceQuantity.FrequencyHz: return FreqSync;
                case ReferenceQuantity.DcVolts: return VoltSync;
                default: return null;
            }
        }
    }

    /// <summary>
    /// A live coupling between a simulated DUT and the seeded simulated reference instruments: the DUT
    /// decorators record each commanded set-point on <see cref="Bench"/> and then <see cref="Apply"/> pushes
    /// it into every registered reference model.
    /// </summary>
    internal sealed class SimulatedBenchCoupling
    {
        private readonly List<Action<SimulatedBench>> _syncs = new List<Action<SimulatedBench>>();
        private readonly List<string> _warnings = new List<string>();

        public SimulatedBench Bench { get; } = new SimulatedBench();

        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// True when at least one reference got a real simulated model. False means every selected
        /// reference fell through to a generic instrument, so the run cannot produce readings — the
        /// coupling then exists only to explain why.
        /// </summary>
        public bool AnySeeded { get; internal set; }

        internal void AddSync(Action<SimulatedBench> sync)
        {
            if (sync != null) _syncs.Add(sync);
        }

        internal void AddWarning(string warning) => _warnings.Add(warning);

        /// <summary>Pushes the current bench state into every seeded reference model.</summary>
        public void Apply()
        {
            foreach (var sync in _syncs) sync(Bench);
        }

        public ISignalSource Couple(ISignalSource source) => new BenchCouplingSource(source, this);

        public IVoltageSourceDut Couple(IVoltageSourceDut dut) => new BenchCouplingVoltageSource(dut, this);

        // ---- DUT decorators ---------------------------------------------------------------------------

        private sealed class BenchCouplingSource : ISignalSource
        {
            private readonly ISignalSource _inner;
            private readonly SimulatedBenchCoupling _coupling;

            public BenchCouplingSource(ISignalSource inner, SimulatedBenchCoupling coupling)
            {
                _inner = inner;
                _coupling = coupling;
            }

            public string ResourceName => _inner.ResourceName;
            public void Initialize() => _inner.Initialize();
            public void Preset() => _inner.Preset();
            public void RfOn() => _inner.RfOn();
            public void RfOff() => _inner.RfOff();

            public void SetFrequencyMHz(double mhz)
            {
                _coupling.Bench.CarrierFrequencyHz = mhz * 1e6;
                _inner.SetFrequencyMHz(mhz);
                _coupling.Apply();
            }

            public void SetPowerDbm(double dbm)
            {
                _coupling.Bench.CarrierPowerDbm = dbm;
                _inner.SetPowerDbm(dbm);
                _coupling.Apply();
            }
        }

        private sealed class BenchCouplingVoltageSource : IVoltageSourceDut
        {
            private readonly IVoltageSourceDut _inner;
            private readonly SimulatedBenchCoupling _coupling;

            public BenchCouplingVoltageSource(IVoltageSourceDut inner, SimulatedBenchCoupling coupling)
            {
                _inner = inner;
                _coupling = coupling;
            }

            public string DisplayName => _inner.DisplayName;
            public void EnableOutput() => _inner.EnableOutput();
            public void DisableOutput() => _inner.DisableOutput();

            public void SetVolts(double volts)
            {
                _coupling.Bench.SourceVolts = volts;
                _inner.SetVolts(volts);
                _coupling.Apply();
            }
        }
    }

    /// <summary>
    /// Turns <c>--provider Simulated</c> into a usable demo bench for the verification harness.
    ///
    /// <para>Without this, a harness run against the Simulated provider errors or stalls:
    /// <see cref="SimulatedGpibProvider"/> auto-creates a *generic* instrument for any unregistered address,
    /// and the specific <c>*SimulatedDevice</c> models are only ever built inside unit tests — so nothing
    /// raises the status bits the reference drivers' completion handshakes wait on, and they (correctly)
    /// time out.</para>
    ///
    /// <para><see cref="TrySeed"/> registers the right model at each reference's resolved resource
    /// <b>before</b> the references are opened (otherwise the provider's auto-create wins), and wires each
    /// model to the shared <see cref="SimulatedBench"/>. The DUT's own address is deliberately left generic:
    /// it only absorbs writes.</para>
    ///
    /// <para><b>The coupling is exact</b> — measured == commanded — so every graded point PASSes by
    /// construction. It exercises the harness end to end; it does not judge an instrument.</para>
    /// </summary>
    internal static class SimulatedHarnessBench
    {
        /// <summary>
        /// Catalog key → factory building that model and its role hooks. Keys absent here have no simulator
        /// model in the suite; they fall through to the generic instrument and are warned about.
        /// </summary>
        private static readonly Dictionary<string, Func<SimulatedBench, SimReferenceModel>> Factories =
            new Dictionary<string, Func<SimulatedBench, SimReferenceModel>>(StringComparer.OrdinalIgnoreCase)
            {
                ["e4418b"] = _ =>
                {
                    var d = new HpE4418BSimulatedDevice();
                    return new SimReferenceModel(d.Instrument)
                    {
                        PowerSync = b => d.PowerDbm = b.CarrierPowerDbm
                    };
                },
                ["hp438a"] = _ =>
                {
                    var d = new Hp438ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument)
                    {
                        PowerSync = b => d.PowerDbmA = b.CarrierPowerDbm
                    };
                },
                ["hp8902a"] = Hp8902AModel,
                ["hp8560e"] = _ =>
                {
                    var d = new Hp8560ESimulatedDevice();
                    return new SimReferenceModel(d.Instrument)
                    {
                        PowerSync = b => SeedAnalyzerAmplitude(b, v => d.Trace = v, v => d.MarkerAmplitudeDbm = v),
                        FreqSync = b => d.MarkerFrequencyHz = b.CarrierFrequencyHz
                    };
                },
                ["hp8591e"] = _ =>
                {
                    var d = new Hp8591ESimulatedDevice();
                    return new SimReferenceModel(d.Instrument)
                    {
                        PowerSync = b => SeedAnalyzerAmplitude(b, v => d.Trace = v, v => d.MarkerAmplitudeDbm = v),
                        FreqSync = b => d.MarkerFrequencyHz = b.CarrierFrequencyHz
                    };
                },
                ["hp53131a"] = _ =>
                {
                    var d = new Hp53131ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument) { FreqSync = b => d.Frequency = b.CarrierFrequencyHz };
                },
                ["hp5342a"] = _ =>
                {
                    var d = new Hp5342ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument) { FreqSync = b => d.Frequency = b.CarrierFrequencyHz };
                },
                ["hp5351a"] = _ =>
                {
                    var d = new Hp5351ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument) { FreqSync = b => d.Frequency = b.CarrierFrequencyHz };
                },
                ["hp34401a"] = _ =>
                {
                    var d = new Hp34401ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument) { VoltSync = b => d.Reading = b.SourceVolts };
                },
                ["hp3458a"] = _ =>
                {
                    var d = new Hp3458ASimulatedDevice();
                    return new SimReferenceModel(d.Instrument) { VoltSync = b => d.Reading = b.SourceVolts };
                },
                ["dm3058"] = _ =>
                {
                    var d = new RigolDm3058SimulatedDevice();
                    return new SimReferenceModel(d.Instrument)
                    {
                        VoltSync = b => d.SetReading(MeasurementFunction.DcVoltage, b.SourceVolts)
                    };
                },
            };

        /// <summary>
        /// Seeds a simulated model for each reference in <paramref name="requests"/>, deduped by resolved
        /// resource. Returns null when <paramref name="provider"/> is not the Simulated provider, or when
        /// nothing could be seeded.
        /// </summary>
        public static SimulatedBenchCoupling TrySeed(IGpibProvider provider, IEnumerable<SimReferenceRequest> requests)
        {
            var sim = provider as SimulatedGpibProvider;
            if (sim == null || requests == null) return null;

            var coupling = new SimulatedBenchCoupling();
            // One model per resource: the same instrument filling two roles (an 8902A doing both power and
            // frequency) must be seeded once, with both role hooks registered against it.
            var seeded = new Dictionary<string, SimReferenceModel>(StringComparer.OrdinalIgnoreCase);
            var seededKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool any = false;

            foreach (var request in requests)
            {
                if (request?.Choice == null || string.IsNullOrWhiteSpace(request.Resource)) continue;
                var key = request.Choice.Key;

                Func<SimulatedBench, SimReferenceModel> factory;
                if (!Factories.TryGetValue(key, out factory))
                {
                    coupling.AddWarning(
                        $"{request.Choice.Description} ({key}) has no simulated model — it will not respond under the Simulated provider.");
                    continue;
                }

                SimReferenceModel model;
                if (seeded.TryGetValue(request.Resource, out model))
                {
                    // Same resource already seeded. Only legitimate when it is the same instrument in a
                    // second role; two different instruments sharing an address is a user mistake.
                    if (!string.Equals(seededKey[request.Resource], key, StringComparison.OrdinalIgnoreCase))
                    {
                        coupling.AddWarning(
                            $"{request.Choice.Description} ({key}) resolves to {request.Resource}, already used by " +
                            $"'{seededKey[request.Resource]}' — only the first is simulated.");
                        continue;
                    }
                }
                else
                {
                    model = factory(coupling.Bench);
                    sim.Add(request.Resource, model.Instrument);
                    seeded[request.Resource] = model;
                    seededKey[request.Resource] = key;
                    any = true;
                }

                if (!model.Supports(request.Choice.Quantity))
                    coupling.AddWarning($"{request.Choice.Description} ({key}) cannot supply {request.Choice.Quantity} in simulation.");
                else
                    coupling.AddSync(model.SyncFor(request.Choice.Quantity));   // null for self-syncing models
            }

            // Keep the coupling even when nothing was seeded, so the warnings still reach the user —
            // otherwise a reference with no simulated model surfaces only as a raw driver error.
            if (!any && coupling.Warnings.Count == 0) return null;
            coupling.AnySeeded = any;
            if (any) coupling.Apply();     // prime the models before the first Prepare/Measure
            return coupling;
        }

        /// <summary>
        /// The 8560E/8591E marker amplitude cannot be seeded directly: the driver sends <c>MKPK HI</c> and
        /// the simulator recomputes <c>MarkerAmplitudeDbm</c> from the peak of <c>Trace</c> at read time. So
        /// the trace is what must carry the level (setting the marker too is harmless belt-and-braces).
        /// </summary>
        private static void SeedAnalyzerAmplitude(SimulatedBench bench, Action<double[]> setTrace, Action<double> setMarker)
        {
            setTrace(new[] { bench.CarrierPowerDbm });
            setMarker(bench.CarrierPowerDbm);
        }

        /// <summary>
        /// The 8902A answers every settled read from a single <c>Reading</c> property whose units depend on
        /// the selected mode — watts for RF power (the driver converts via <c>Rf.WattsToDbm</c>) but Hz for
        /// frequency. A set-point-time hook cannot serve both when one receiver fills both roles, so resolve
        /// it at <b>read</b> time instead: the simulator exposes <c>Mode</c> ("M5" = frequency), and the
        /// session invokes <c>Responder</c> on the read, after the mode-selecting write.
        /// </summary>
        private static SimReferenceModel Hp8902AModel(SimulatedBench bench)
        {
            var d = new Hp8902ASimulatedDevice();
            var inner = d.Instrument.Responder;
            d.Instrument.Responder = command =>
            {
                double value = d.Mode == "M5"
                    ? bench.CarrierFrequencyHz
                    : Rf.DbmToWatts(bench.CarrierPowerDbm);

                // Drive ReadingOverride, not Reading: the model renders Reading as watts with "0.######",
                // which rounds anything below about -30 dBm (1e-7 W) to zero and yields NaN dBm. Emitting
                // the string ourselves at round-trip precision keeps low-level points meaningful. The
                // driver parses with NumberStyles.Float, so exponent notation is fine.
                d.ReadingOverride = value.ToString("G17", CultureInfo.InvariantCulture);
                return inner?.Invoke(command);
            };
            // No set-point hooks: the closure above reads the bench directly at measurement time, so both
            // roles are covered without one.
            var model = new SimReferenceModel(d.Instrument);
            model.SelfSyncedQuantities.Add(ReferenceQuantity.RfPowerDbm);
            model.SelfSyncedQuantities.Add(ReferenceQuantity.FrequencyHz);
            return model;
        }
    }
}
