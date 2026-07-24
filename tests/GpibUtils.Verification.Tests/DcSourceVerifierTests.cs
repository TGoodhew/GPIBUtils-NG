using System;
using System.Globalization;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.Meters;
using GpibUtils.Verification.References;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    public class DcSourceVerifierTests
    {
        private static VerificationPoint V(double volts, double? tol = null, string range = null) =>
            new VerificationPoint { NominalVolts = volts, TolerancePpm = tol, Range = range };

        [Fact]
        public void Rejects_a_non_voltage_reference()
        {
            var wrong = new FakeReference(ReferenceQuantity.RfPowerDbm, "dBm", 0.0);
            Assert.Throws<ArgumentException>(() => new DcSourceVerifier(new FakeVoltageSourceDut(), wrong));
        }

        [Fact]
        public void Within_tolerance_passes_and_drives_the_dut()
        {
            var dut = new FakeVoltageSourceDut();
            var vref = new FakeReference(ReferenceQuantity.DcVolts, "V", 10.0);
            var results = new DcSourceVerifier(dut, vref, new DcSourceOptions { SettlingMs = 0 })
                .Run(new[] { V(10, tol: 50) });

            Assert.Equal("PASS", results[0].Verdict);
            Assert.Equal(10.0, dut.LastVolts, 6);
            Assert.Equal(10.0, results[0].MeasuredVolts, 6);
        }

        [Fact]
        public void Out_of_tolerance_fails()
        {
            var vref = new FakeReference(ReferenceQuantity.DcVolts, "V", 10.001);   // +1 mV on 10 V = 100 ppm
            var results = new DcSourceVerifier(new FakeVoltageSourceDut(), vref, new DcSourceOptions { SettlingMs = 0 })
                .Run(new[] { V(10, tol: 50) });
            Assert.Equal("FAIL", results[0].Verdict);
            Assert.Equal(100, results[0].PpmOfReading, 0);
        }

        [Fact]
        public void Point_that_throws_is_recorded_as_error_and_the_run_continues()
        {
            var dut = new FakeVoltageSourceDut();
            // The 10 V point's reference read throws; the 5 V points read fine.
            var vref = new ThrowingReference(ReferenceQuantity.DcVolts, "V", 5.0, level => level == 10);
            var results = new DcSourceVerifier(dut, vref, new DcSourceOptions { SettlingMs = 0, DefaultTolerancePpm = 50 })
                .Run(new[] { V(5), V(10), V(5) });

            Assert.Equal(3, results.Count);                    // nothing dropped
            Assert.Equal("ERROR", results[1].Verdict);
            Assert.True(results[1].Errored);
            Assert.NotNull(results[1].Error);
            Assert.False(results[1].Passed);
            Assert.False(results[1].Failed);
            Assert.True(double.IsNaN(results[1].MeasuredVolts));
            Assert.NotEqual("ERROR", results[0].Verdict);      // completed points survive the mid-run throw
            Assert.NotEqual("ERROR", results[2].Verdict);
            Assert.False(dut.OutputEnabled);                   // output disabled on exit despite the error
            Assert.True(dut.DisableCount >= 1);
        }

        [Fact]
        public void No_tolerance_is_report_only()
        {
            var vref = new FakeReference(ReferenceQuantity.DcVolts, "V", 5.0);
            var results = new DcSourceVerifier(new FakeVoltageSourceDut(), vref, new DcSourceOptions { SettlingMs = 0 })
                .Run(new[] { V(5) });
            Assert.Null(results[0].Verdict);
        }

        [Fact]
        public void Disables_output_on_exit()
        {
            var dut = new FakeVoltageSourceDut();
            var vref = new FakeReference(ReferenceQuantity.DcVolts, "V", 1.0);
            new DcSourceVerifier(dut, vref, new DcSourceOptions { SettlingMs = 0, DisableOutputOnExit = true })
                .Run(new[] { V(1) });
            Assert.False(dut.OutputEnabled);
            Assert.Equal(1, dut.DisableCount);
        }

        [Fact]
        public void At_zero_uses_full_scale_for_the_verdict()
        {
            var vref = new FakeReference(ReferenceQuantity.DcVolts, "V", 0.001);   // 1 mV err on 10 V FS = 100 ppm
            var results = new DcSourceVerifier(new FakeVoltageSourceDut(), vref, new DcSourceOptions { SettlingMs = 0 })
                .Run(new[] { V(0, tol: 50, range: "10") });
            Assert.Equal("FAIL", results[0].Verdict);
            Assert.True(double.IsNaN(results[0].PpmOfReading));
        }

        // ---- sim-green integration: a real Fluke 5440 verified by a real 34401A ----

        [Fact]
        public void Simulated_5440_verified_by_34401A()
        {
            var provider = new SimulatedGpibProvider();
            var cal = new Fluke5440ASimulatedDevice();
            var dmm = new Hp34401ASimulatedDevice();
            provider.Add("GPIB0::7::INSTR", cal.Instrument);
            provider.Add("GPIB0::22::INSTR", dmm.Instrument);
            dmm.Instrument.Responder = command =>
            {
                var u = (command ?? string.Empty).Trim().ToUpperInvariant();
                if (u == "READ?" || u == "FETCH?" || u == "FETC?")
                    return cal.OutputVolts.ToString("G12", CultureInfo.InvariantCulture);
                return null;
            };

            using (var calSession = provider.Open("GPIB0::7::INSTR"))
            using (var dmmSession = provider.Open("GPIB0::22::INSTR"))
            {
                var dut = new CalibratorVoltageDut(new Fluke5440A(calSession), "Fluke 5440A");
                var vref = new DmmVoltageReference(new Hp34401A(dmmSession), dmmSession, "HP 34401A");
                var verifier = new DcSourceVerifier(dut, vref, new DcSourceOptions { SettlingMs = 0, Samples = 2 });

                var results = verifier.Run(new[] { V(1, tol: 50), V(10, tol: 50) }, _ => { });

                Assert.All(results, r => Assert.Equal("PASS", r.Verdict));
                Assert.Equal(10.0, results[1].MeasuredVolts, 4);
            }
        }
    }
}
