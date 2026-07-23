using System.Collections.Generic;
using GpibUtils.Instruments.Analyzers;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification;
using GpibUtils.Verification.References;

namespace GpibUtils.Verification.Tests
{
    /// <summary>A spectrum analyzer that records tuning and returns a fixed peak amplitude/frequency.</summary>
    internal sealed class FakeSpectrumAnalyzer : ISpectrumAnalyzer
    {
        private readonly double _peakDbm;
        private readonly double _peakHz;

        public FakeSpectrumAnalyzer(double peakDbm, double peakHz) { _peakDbm = peakDbm; _peakHz = peakHz; }

        public string ResourceName => "FAKE::SA";
        public double LastCenterHz { get; private set; }
        public double LastSpanHz { get; private set; }
        public int Sweeps { get; private set; }

        public string Identify() => "FAKE,SA,0,1";
        public void Initialize() { }
        public void Preset() { }
        public void SetCenterFrequencyHz(double hertz) => LastCenterHz = hertz;
        public void SetSpanHz(double hertz) => LastSpanHz = hertz;
        public void SingleSweep() => Sweeps++;
        public IReadOnlyList<double> ReadTrace() => new[] { _peakDbm };
        public double MarkerToPeakAmplitude() => _peakDbm;
        public double MarkerFrequencyHz() => _peakHz;
    }

    /// <summary>A signal source that records what it was driven to, for deterministic verifier logic tests.</summary>
    internal sealed class FakeSignalSource : ISignalSource
    {
        public string ResourceName => "FAKE::SOURCE";
        public double LastFrequencyMHz { get; private set; }
        public double LastPowerDbm { get; private set; }
        public bool RfIsOn { get; private set; }
        public int RfOffCount { get; private set; }

        public void Initialize() { }
        public void Preset() { }
        public void SetFrequencyMHz(double mhz) => LastFrequencyMHz = mhz;
        public void SetPowerDbm(double dbm) => LastPowerDbm = dbm;
        public void RfOn() => RfIsOn = true;
        public void RfOff() { RfIsOn = false; RfOffCount++; }
    }

    /// <summary>A voltage-source DUT that records what it was driven to.</summary>
    internal sealed class FakeVoltageSourceDut : IVoltageSourceDut
    {
        public string DisplayName => "fake DC source";
        public double LastVolts { get; private set; }
        public bool OutputEnabled { get; private set; }
        public int DisableCount { get; private set; }

        public void SetVolts(double volts) => LastVolts = volts;
        public void EnableOutput() => OutputEnabled = true;
        public void DisableOutput() { OutputEnabled = false; DisableCount++; }
    }

    /// <summary>A reference that returns a fixed value or a scripted sequence, and records preparation.</summary>
    internal sealed class FakeReference : IReferenceMeasurement
    {
        private readonly double _fixed;
        private readonly Queue<double> _values;

        public FakeReference(ReferenceQuantity quantity, string unit, double value)
        {
            Quantity = quantity; Unit = unit; _fixed = value; DisplayName = "fake reference";
        }

        public FakeReference(ReferenceQuantity quantity, string unit, IEnumerable<double> values)
        {
            Quantity = quantity; Unit = unit; _values = new Queue<double>(values); DisplayName = "fake reference";
        }

        public string DisplayName { get; }
        public ReferenceQuantity Quantity { get; }
        public string Unit { get; }

        public int PrepareCalls { get; private set; }
        public ReferencePoint LastPoint { get; private set; }

        public void Prepare(ReferencePoint point) { PrepareCalls++; LastPoint = point; }
        public double Measure() => _values != null ? _values.Dequeue() : _fixed;
        public void Dispose() { }
    }
}
