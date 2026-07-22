using System;
using GpibUtils.Instruments.Meters;
using GpibUtils.Visa;

namespace GpibUtils.Verification.References
{
    /// <summary>
    /// RF-power reference backed by an HP 8902A measuring receiver (<see cref="IMeasuringReceiver"/>) — the
    /// canonical way to verify a signal generator's absolute output level. Each point re-begins a Tuned
    /// RF-Power measurement at the carrier frequency (so the correct cal factor applies) and reads the
    /// settled power in dBm.
    /// </summary>
    public sealed class MeasuringReceiverPowerReference : IReferenceMeasurement
    {
        private readonly IMeasuringReceiver _receiver;
        private readonly IDisposable _owned;
        private readonly MeasurementRegime _regime;
        private readonly double _loMHz;
        private bool _selected;

        public MeasuringReceiverPowerReference(IMeasuringReceiver receiver, IDisposable ownedSession = null,
            string displayName = null, MeasurementRegime regime = MeasurementRegime.Direct, double loMHz = 0)
        {
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            _owned = ownedSession;
            _regime = regime;
            _loMHz = loMHz;
            DisplayName = displayName ?? "HP 8902A measuring receiver";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.RfPowerDbm;
        public string Unit => "dBm";

        public void Prepare(ReferencePoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (!_selected) { _receiver.SelectRfPower(); _selected = true; }
            _receiver.BeginRfPowerMeasurement(point.FrequencyMHz, _regime, _loMHz);
        }

        public double Measure() => _receiver.ReadRfPowerDbm();

        public void Dispose() => _owned?.Dispose();
    }

    /// <summary>
    /// Frequency reference backed by an HP 8902A measuring receiver: reads the input signal frequency the
    /// receiver has tuned to. Lets the same 8902A that checks a source's power also confirm its frequency.
    /// </summary>
    public sealed class MeasuringReceiverFrequencyReference : IReferenceMeasurement
    {
        private readonly IMeasuringReceiver _receiver;
        private readonly IDisposable _owned;

        public MeasuringReceiverFrequencyReference(IMeasuringReceiver receiver, IDisposable ownedSession = null,
            string displayName = null)
        {
            _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            _owned = ownedSession;
            DisplayName = displayName ?? "HP 8902A measuring receiver";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.FrequencyHz;
        public string Unit => "Hz";

        public void Prepare(ReferencePoint point) { }

        public double Measure() => _receiver.ReadSignalFrequencyMHz() * 1e6;

        public void Dispose() => _owned?.Dispose();
    }

    /// <summary>
    /// RF-power reference backed by a broadband power meter (<see cref="IPowerMeter"/>: HP E4418B, 438A,
    /// 437B, 436A). Zeroes/calibrates the sensor once, then reads absolute power in dBm at each point. When
    /// the backing meter can be tuned to the carrier for cal-factor accuracy (e.g. the E4418B), pass a
    /// <paramref name="setFrequencyMHz"/> hook so <see cref="Prepare"/> applies it.
    /// </summary>
    public sealed class PowerMeterReference : IReferenceMeasurement
    {
        private readonly IPowerMeter _meter;
        private readonly IDisposable _owned;
        private readonly Action<double> _setFrequencyMHz;
        private bool _calibrated;

        public PowerMeterReference(IPowerMeter meter, IDisposable ownedSession = null, string displayName = null,
            Action<double> setFrequencyMHz = null)
        {
            _meter = meter ?? throw new ArgumentNullException(nameof(meter));
            _owned = ownedSession;
            _setFrequencyMHz = setFrequencyMHz;
            DisplayName = displayName ?? _meter.Identify();
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity => ReferenceQuantity.RfPowerDbm;
        public string Unit => "dBm";

        public void Prepare(ReferencePoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            if (!_calibrated) { _meter.ZeroAndCalibrate(); _calibrated = true; }
            _setFrequencyMHz?.Invoke(point.FrequencyMHz);
        }

        public double Measure() => _meter.MeasurePowerDbm();

        public void Dispose() => _owned?.Dispose();
    }
}
