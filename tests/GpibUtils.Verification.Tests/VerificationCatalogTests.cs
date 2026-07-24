using System.Linq;
using GpibUtils.Verification.Catalog;
using GpibUtils.Verification.References;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Verification.Tests
{
    /// <summary>
    /// Smoke-tests the catalog: every DUT/reference factory constructs cleanly over the Simulated provider
    /// and every reference reports the quantity its role declares. This is the guard that all the driver
    /// constructors and reference adapters stay wired up as drivers are added.
    /// </summary>
    public class VerificationCatalogTests
    {
        private const string Res = "GPIB0::1::INSTR";

        private static SimulatedGpibProvider NewProvider()
        {
            var p = new SimulatedGpibProvider();
            p.Add(Res, new SimulatedInstrument { IdentificationString = "SIM,DEV,0,1" });
            return p;
        }

        [Fact]
        public void Rf_power_reference_factories_build_dbm_references()
        {
            var p = NewProvider();
            Assert.NotEmpty(VerificationCatalog.RfPowerReferences);
            foreach (var c in VerificationCatalog.RfPowerReferences)
            {
                var r = c.Open(p.Open(Res));
                Assert.Equal(ReferenceQuantity.RfPowerDbm, r.Quantity);
                Assert.Equal(ReferenceQuantity.RfPowerDbm, c.Quantity);
                Assert.Equal("dBm", r.Unit);
                Assert.False(string.IsNullOrWhiteSpace(r.DisplayName));
                r.Dispose();
            }
        }

        [Fact]
        public void Frequency_reference_factories_build_hz_references()
        {
            var p = NewProvider();
            Assert.NotEmpty(VerificationCatalog.FrequencyReferences);
            foreach (var c in VerificationCatalog.FrequencyReferences)
            {
                var r = c.Open(p.Open(Res));
                Assert.Equal(ReferenceQuantity.FrequencyHz, r.Quantity);
                Assert.Equal("Hz", r.Unit);
                r.Dispose();
            }
        }

        [Fact]
        public void Dc_voltage_reference_factories_build_volt_references()
        {
            var p = NewProvider();
            Assert.NotEmpty(VerificationCatalog.DcVoltageReferences);
            foreach (var c in VerificationCatalog.DcVoltageReferences)
            {
                var r = c.Open(p.Open(Res));
                Assert.Equal(ReferenceQuantity.DcVolts, r.Quantity);
                Assert.Equal("V", r.Unit);
                r.Dispose();
            }
        }

        [Fact]
        public void Signal_source_dut_factories_build_sources()
        {
            var p = NewProvider();
            Assert.NotEmpty(VerificationCatalog.SignalSourceDuts);
            foreach (var d in VerificationCatalog.SignalSourceDuts)
                using (var s = p.Open(Res))
                    Assert.NotNull(d.Open(s));
        }

        [Fact]
        public void Dc_source_dut_factories_build_sources()
        {
            var p = NewProvider();
            Assert.NotEmpty(VerificationCatalog.DcSourceDuts);
            foreach (var d in VerificationCatalog.DcSourceDuts)
                using (var s = p.Open(Res))
                    Assert.NotNull(d.Open(s));
        }

        [Fact]
        public void Reference_keys_are_unique_within_each_role()
        {
            AssertUniqueKeys(VerificationCatalog.RfPowerReferences.Select(r => r.Key));
            AssertUniqueKeys(VerificationCatalog.FrequencyReferences.Select(r => r.Key));
            AssertUniqueKeys(VerificationCatalog.DcVoltageReferences.Select(r => r.Key));
            AssertUniqueKeys(VerificationCatalog.SignalSourceDuts.Select(d => d.Key));
            AssertUniqueKeys(VerificationCatalog.DcSourceDuts.Select(d => d.Key));
        }

        private static void AssertUniqueKeys(System.Collections.Generic.IEnumerable<string> keys)
        {
            var list = keys.ToList();
            Assert.Equal(list.Count, list.Distinct().Count());
        }

        // Guards the reference-key sets the verify-harness --power-ref / --freq-ref help advertises (#229):
        // the two spectrum analyzers must stay registered, or the help drifts back out of sync with reality.
        [Fact]
        public void Rf_power_references_include_the_spectrum_analyzers()
        {
            var keys = VerificationCatalog.RfPowerReferences.Select(r => r.Key).ToList();
            Assert.Contains("hp8560e", keys);
            Assert.Contains("hp8591e", keys);
        }

        [Fact]
        public void Frequency_references_include_the_spectrum_analyzers()
        {
            var keys = VerificationCatalog.FrequencyReferences.Select(r => r.Key).ToList();
            Assert.Contains("hp8560e", keys);
            Assert.Contains("hp8591e", keys);
        }
    }
}
