using System;
using System.Collections.Generic;
using System.Linq;

namespace GpibUtils.Visa.Simulation
{
    /// <summary>
    /// An in-memory provider that opens <see cref="SimulatedInstrument"/>s instead of real hardware.
    /// It lets the suite build, run its tests, and demo drivers on CI with no VISA runtime or bench.
    ///
    /// Register instruments with <see cref="Add"/>; by default an unknown resource auto-creates a
    /// generic instrument (set <see cref="AutoCreate"/> to false to make unknown resources throw).
    /// </summary>
    public sealed class SimulatedGpibProvider : IGpibProvider
    {
        private readonly Dictionary<string, SimulatedInstrument> _instruments =
            new Dictionary<string, SimulatedInstrument>(StringComparer.OrdinalIgnoreCase);

        public string Name => "Simulated";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "Simulated", discovery: true, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        public bool IsAvailable => true;
        public string UnavailableReason => null;

        /// <summary>When true (default), opening an unregistered resource creates a generic instrument.</summary>
        public bool AutoCreate { get; set; } = true;

        /// <summary>Registers (or replaces) a simulated instrument at <paramref name="resourceName"/>.</summary>
        public SimulatedInstrument Add(string resourceName, SimulatedInstrument instrument = null)
        {
            instrument = instrument ?? new SimulatedInstrument();
            _instruments[resourceName] = instrument;
            return instrument;
        }

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => _instruments.Keys.ToList();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Resource name must be provided.", nameof(resourceName));

            if (!_instruments.TryGetValue(resourceName, out var instrument))
            {
                if (!AutoCreate)
                    throw new GpibException($"No simulated instrument registered at '{resourceName}'.");
                instrument = Add(resourceName);
            }
            return new SimulatedInstrumentSession(this, resourceName, instrument, settings);
        }

        public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
    }
}
