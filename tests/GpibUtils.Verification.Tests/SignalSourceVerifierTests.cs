using System;
using System.Linq;
using GpibUtils.Instruments.Meters;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Verification.References;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    public class SignalSourceVerifierTests
    {
        private static SignalSourcePoint P(double freqMHz, double powerDbm) =>
            new SignalSourcePoint { FrequencyMHz = freqMHz, PowerDbm = powerDbm };

        [Fact]
        public void Requires_at_least_one_reference()
        {
            Assert.Throws<ArgumentException>(() =>
                new SignalSourceVerifier(new FakeSignalSource(), null, null));
        }

        [Fact]
        public void Rejects_a_reference_of_the_wrong_quantity()
        {
            var wrong = new FakeReference(ReferenceQuantity.DcVolts, "V", 0.0);
            Assert.Throws<ArgumentException>(() =>
                new SignalSourceVerifier(new FakeSignalSource(), wrong, null));
        }

        [Fact]
        public void Power_within_tolerance_passes_and_drives_the_source()
        {
            var src = new FakeSignalSource();
            var pref = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", 0.0);
            var v = new SignalSourceVerifier(src, pref, null,
                new SignalSourceOptions { SettlingMs = 0, Samples = 4, DefaultPowerToleranceDb = 1.0 });

            var results = v.Run(new[] { P(1000, 0) });

            Assert.Single(results);
            Assert.Equal("PASS", results[0].PowerVerdict);
            Assert.True(results[0].Passed);
            Assert.Equal(0, results[0].PowerErrorDb, 6);
            Assert.Equal(1000, src.LastFrequencyMHz);
            Assert.Equal(0, src.LastPowerDbm);
            Assert.Equal(1000, pref.LastPoint.FrequencyMHz);   // reference told the carrier
        }

        [Fact]
        public void Power_out_of_tolerance_fails()
        {
            var pref = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", 0.0);   // reads 0 dBm
            var v = new SignalSourceVerifier(new FakeSignalSource(), pref, null,
                new SignalSourceOptions { SettlingMs = 0, DefaultPowerToleranceDb = 1.0 });

            var results = v.Run(new[] { P(1000, 10) });   // commanded 10 dBm, measured 0 -> -10 dB error

            Assert.Equal("FAIL", results[0].PowerVerdict);
            Assert.True(results[0].Failed);
            Assert.Equal(-10, results[0].PowerErrorDb, 6);
        }

        [Fact]
        public void Frequency_ppm_grading()
        {
            // target 1000 MHz = 1e9 Hz; reference reads 1e9 + 1000 Hz = +1 ppm.
            var fref = new FakeReference(ReferenceQuantity.FrequencyHz, "Hz", 1_000_001_000.0);
            var pass = new SignalSourceVerifier(new FakeSignalSource(), null, fref,
                new SignalSourceOptions { SettlingMs = 0, DefaultFrequencyTolerancePpm = 2.0 }).Run(new[] { P(1000, 0) });
            Assert.Equal("PASS", pass[0].FrequencyVerdict);
            Assert.Equal(1.0, pass[0].FrequencyErrorPpm, 3);

            var fref2 = new FakeReference(ReferenceQuantity.FrequencyHz, "Hz", 1_000_001_000.0);
            var fail = new SignalSourceVerifier(new FakeSignalSource(), null, fref2,
                new SignalSourceOptions { SettlingMs = 0, DefaultFrequencyTolerancePpm = 0.5 }).Run(new[] { P(1000, 0) });
            Assert.Equal("FAIL", fail[0].FrequencyVerdict);
        }

        [Fact]
        public void No_tolerance_is_report_only()
        {
            var pref = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", -3.0);
            var results = new SignalSourceVerifier(new FakeSignalSource(), pref, null,
                new SignalSourceOptions { SettlingMs = 0 }).Run(new[] { P(1000, 0) });

            Assert.Null(results[0].PowerVerdict);
            Assert.False(results[0].Passed);
            Assert.False(results[0].Failed);
            Assert.True(results[0].PowerMeasured);
            Assert.Equal(-3.0, results[0].MeasuredPowerDbm, 6);
        }

        [Fact]
        public void Averages_samples_per_point()
        {
            var pref = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", new[] { -1.0, 1.0, -1.0, 1.0 });
            var results = new SignalSourceVerifier(new FakeSignalSource(), pref, null,
                new SignalSourceOptions { SettlingMs = 0, Samples = 4 }).Run(new[] { P(1000, 0) });
            Assert.Equal(0.0, results[0].MeasuredPowerDbm, 6);
            Assert.Equal(4, results[0].Samples);
        }

        [Fact]
        public void Turns_rf_off_on_exit()
        {
            var src = new FakeSignalSource();
            var pref = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", 0.0);
            new SignalSourceVerifier(src, pref, null, new SignalSourceOptions { SettlingMs = 0 }).Run(new[] { P(1000, 0) });
            Assert.False(src.RfIsOn);
            Assert.Equal(1, src.RfOffCount);
        }

        // ---- sim-green integration: a real 8340B source verified by a real 8902A receiver ----

        [Fact]
        public void Simulated_8340B_verified_by_8902A_power_reference()
        {
            var provider = new SimulatedGpibProvider();
            var source = new Hp8340BSimulatedDevice();
            var receiver = new Hp8902ASimulatedDevice { Reading = 1e-3 };   // 1 mW = 0 dBm
            provider.Add("GPIB0::19::INSTR", source.Instrument);
            provider.Add("GPIB0::14::INSTR", receiver.Instrument);

            using (var srcSession = provider.Open("GPIB0::19::INSTR"))
            using (var recvSession = provider.Open("GPIB0::14::INSTR"))
            {
                var dut = new Hp8340B(srcSession);
                var powerRef = new MeasuringReceiverPowerReference(new Hp8902A(recvSession), recvSession, "HP 8902A");
                var verifier = new SignalSourceVerifier(dut, powerRef, null,
                    new SignalSourceOptions { SettlingMs = 0, Samples = 2, DefaultPowerToleranceDb = 1.0 });

                var results = verifier.Run(new[] { P(1000, 0) }, _ => { });

                Assert.Equal("PASS", results[0].PowerVerdict);
                Assert.Equal(0.0, results[0].MeasuredPowerDbm, 3);
                Assert.Equal(1000, source.FrequencyMHz);
                Assert.Equal(0, source.PowerDbm);
            }
        }
    }
}
