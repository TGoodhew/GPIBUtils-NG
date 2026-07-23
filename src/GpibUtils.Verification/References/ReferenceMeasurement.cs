using System;

namespace GpibUtils.Verification.References
{
    /// <summary>
    /// The physical quantity a reference instrument measures when it verifies a device under test.
    /// Used by the <see cref="Catalog.VerificationCatalog"/> to match a DUT to the instruments that can
    /// verify it, and by the interactive harness to let the user pick between equivalent references.
    /// </summary>
    public enum ReferenceQuantity
    {
        /// <summary>Absolute RF power / level, in dBm (power meters, the 8902A measuring receiver).</summary>
        RfPowerDbm,

        /// <summary>Signal frequency, in Hz (frequency counters, the 8902A).</summary>
        FrequencyHz,

        /// <summary>DC voltage, in volts (DMMs, an SMU).</summary>
        DcVolts
    }

    /// <summary>
    /// A "reference" measuring instrument used to verify a device under test: it observes the DUT's output
    /// and reports the actual value of one <see cref="Quantity"/>. Adapters wrap a concrete driver
    /// (8902A, a power meter, a counter, a DMM, …) so the verification runners can treat every reference
    /// uniformly. All adapters run over the shared VISA transport, so a reference is provider-agnostic.
    /// <para>
    /// Note: the adapters are unit-tested hardware-free against fakes, but a whole verification run is
    /// <b>not</b> yet drivable end-to-end against the <c>Simulated</c> provider — the simulator
    /// auto-creates a *generic* instrument for any unregistered address, which never raises the status
    /// bits these reference drivers' completion handshakes wait on. See <c>docs/SIM_BENCH_PLAN.md</c>.
    /// </para>
    /// </summary>
    public interface IReferenceMeasurement : IDisposable
    {
        /// <summary>Human-readable name of the backing instrument (e.g. "HP 8902A measuring receiver").</summary>
        string DisplayName { get; }

        /// <summary>The quantity this reference measures.</summary>
        ReferenceQuantity Quantity { get; }

        /// <summary>The engineering unit of <see cref="Measure"/> ("dBm", "Hz", or "V").</summary>
        string Unit { get; }

        /// <summary>
        /// Prepares the reference for the DUT's next operating point. Power references use the carrier
        /// frequency to pick the right cal factor; other references may ignore it. Called once per point
        /// before <see cref="Measure"/>.
        /// </summary>
        void Prepare(ReferencePoint point);

        /// <summary>Takes one settled reading of <see cref="Quantity"/> in <see cref="Unit"/>.</summary>
        double Measure();
    }

    /// <summary>The DUT operating point a reference is being asked to measure.</summary>
    public sealed class ReferencePoint
    {
        /// <summary>Carrier / signal frequency the DUT is set to, in MHz (used for power cal-factor lookup).</summary>
        public double FrequencyMHz { get; set; }

        /// <summary>Nominal level the DUT is set to (dBm for RF, V for DC), for context/logging.</summary>
        public double NominalLevel { get; set; }
    }
}
