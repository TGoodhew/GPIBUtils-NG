using System.Collections.Generic;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Instruments.Switches;

namespace GpibUtils.Measurement.Tests
{
    /// <summary>
    /// A deterministic, hardware-free bench for the <see cref="MeasurementEngine"/>: an ideal attenuator +
    /// receiver where the receiver reads exactly the commanded attenuation (relative dB = -attenuation), so a
    /// sweep should recover each commanded step with ~zero error. Shared state links the fake attenuator to
    /// the fake receiver.
    /// </summary>
    public sealed class FakeBench
    {
        public double AttenDb;
        public double FreqMHz;

        public FakeSource Source { get; }
        public FakeLo Lo { get; }
        public FakeAttenuator Attenuator { get; }
        public FakeReceiver Receiver { get; }

        public FakeBench()
        {
            Source = new FakeSource(this);
            Lo = new FakeLo(this);
            Attenuator = new FakeAttenuator(this);
            Receiver = new FakeReceiver(this);
        }
    }

    public sealed class FakeSource : ISignalSource
    {
        private readonly FakeBench _b;
        public FakeSource(FakeBench b) { _b = b; }
        public string ResourceName => "FAKE::SOURCE";
        public void Initialize() { }
        public void Preset() { }
        public void SetFrequencyMHz(double mhz) { _b.FreqMHz = mhz; }
        public void SetPowerDbm(double dbm) { }
        public void RfOn() { }
        public void RfOff() { }
    }

    public sealed class FakeLo : ILocalOscillator
    {
        private readonly FakeBench _b;
        public FakeLo(FakeBench b) { _b = b; }
        public string ResourceName => "FAKE::LO";
        public double MinFrequencyMHz => 2000.0;
        public double MaxFrequencyMHz => 26500.0;
        public void Initialize() { }
        public void Preset() { }
        public void SetFrequencyMHz(double mhz) { }
        public void SetPowerDbm(double dbm) { }
        public void RfOn() { }
        public void RfOff() { }
    }

    public sealed class FakeAttenuator : IStepAttenuator
    {
        private readonly FakeBench _b;
        public FakeAttenuator(FakeBench b) { _b = b; }
        public string ResourceName => "FAKE::ATTEN";
        public AttenuatorConfig Config => null;
        public void Initialize() { _b.AttenDb = 0; }
        public string SetAttenuationDb(int db) { _b.AttenDb = db; return "A" + db; }
        public string SetEngaged(IEnumerable<int> digits) { return "ENG"; }
    }

    /// <summary>Ideal receiver: relative dB = -(commanded attenuation), so measured attenuation == commanded.</summary>
    public sealed class FakeReceiver : IMeasuringReceiver
    {
        private readonly FakeBench _b;
        public FakeReceiver(FakeBench b) { _b = b; }

        public string ResourceName => "FAKE::RECEIVER";
        public void Initialize() { }
        public void Reset() { }
        public void SelectRfPower() { }
        public void LoadCalFactors(double referenceCf, IReadOnlyList<CalFactor> table) { }
        public double ZeroSensor() => 0.0;
        public double CalibrateSensor() => 1e-3;
        public void BeginAttenuationMeasurement(double rfMHz, MeasurementRegime regime, double loMHz,
            TrflDetector detector = TrflDetector.Average, bool trackMode = false, TrflTuning tuning = TrflTuning.Manual) { }
        public void BeginRfPowerMeasurement(double rfMHz, MeasurementRegime regime, double loMHz) { }
        public double ReadRfPowerDbm() => -_b.AttenDb;
        public void BeginRangeCalibration() { }
        public void EnableRecalStatus() { }
        public bool RecalRequested() => false;
        public int PollStatusByte() => 0;
        public void Calibrate() { }
        public void ClearError() { }
        public void RetuneToSignal() { }
        public void ReleaseBus() { }
        public void SetReference() { }
        public double ReadRelativeDb() => -_b.AttenDb;      // ideal: reads the commanded attenuation
        public double ReadTunedLevelDbm() => -_b.AttenDb;
        public double ReadSignalFrequencyMHz() => _b.FreqMHz;
    }
}
