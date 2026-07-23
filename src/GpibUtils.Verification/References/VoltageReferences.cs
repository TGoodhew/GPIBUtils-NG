using System;
using GpibUtils.Instruments.Meters;

namespace GpibUtils.Verification.References
{
    /// <summary>
    /// DC-voltage reference backed by a digital multimeter (<see cref="IDigitalMultimeter"/>: HP 34401A,
    /// HP 3458A via its adapter, Rigol DM3058, Keithley 2015). Configures the DMM for DC volts once, then
    /// reads a settled value in volts at each point — used to verify a DC calibrator or a power supply's
    /// programmed output.
    /// </summary>
    public sealed class DmmVoltageReference : IReferenceMeasurement
    {
        private readonly IDigitalMultimeter _dmm;
        private readonly IDisposable _owned;
        private readonly string _range;
        private bool _configured;

        public DmmVoltageReference(IDigitalMultimeter dmm, IDisposable ownedSession = null,
            string displayName = null, string range = null)
        {
            _dmm = dmm ?? throw new ArgumentNullException(nameof(dmm));
            _owned = ownedSession;
            _range = range;
            DisplayName = displayName ?? "digital multimeter";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.DcVolts;
        public string Unit => "V";

        public void Prepare(ReferencePoint point)
        {
            if (!_configured)
            {
                _dmm.Configure(MeasurementFunction.DcVoltage, _range);
                _configured = true;
            }
        }

        public double Measure() => _dmm.ReadValue();

        public void Dispose() => _owned?.Dispose();
    }

    /// <summary>
    /// DC-voltage reference backed by an HP 3458A 8.5-digit DMM. The 3458A is a standalone driver (its
    /// native command set does not fit <see cref="IDigitalMultimeter"/>), so it gets its own adapter:
    /// configure DC volts once, then read a settled value. The most accurate DC reference on the bench.
    /// </summary>
    public sealed class Hp3458AVoltageReference : IReferenceMeasurement
    {
        private readonly Hp3458A _dmm;
        private readonly IDisposable _owned;
        private bool _configured;

        public Hp3458AVoltageReference(Hp3458A dmm, IDisposable ownedSession = null, string displayName = null)
        {
            _dmm = dmm ?? throw new ArgumentNullException(nameof(dmm));
            _owned = ownedSession;
            DisplayName = displayName ?? "HP 3458A DMM";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.DcVolts;
        public string Unit => "V";

        public void Prepare(ReferencePoint point)
        {
            if (!_configured) { _dmm.ConfigureFunction(Hp3458AFunction.DcVoltage); _configured = true; }
        }

        public double Measure() => _dmm.ReadValue();

        public void Dispose() => _owned?.Dispose();
    }
}
