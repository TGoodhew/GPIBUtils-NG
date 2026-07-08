using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa;
using Xunit;

namespace GpibUtils.Visa.Tests
{
    public class GpibProvidersTests
    {
        [Fact]
        public void BuiltIn_providers_are_registered()
        {
            var names = GpibProviders.Names;
            Assert.Contains("NI-VISA", names);
            Assert.Contains("NI-488.2", names);
            Assert.Contains("Keysight-VISA", names);
            Assert.Contains("Prologix", names);
            Assert.Contains("AR488", names);
            Assert.Contains("Simulated", names);
        }

        [Fact]
        public void Default_provider_is_ni_visa()
        {
            Assert.Equal("NI-VISA", GpibProviders.Default.Name);
        }

        [Fact]
        public void Get_is_case_insensitive()
        {
            Assert.Same(GpibProviders.Get("ni-visa"), GpibProviders.Get("NI-VISA"));
        }

        [Fact]
        public void Get_unknown_throws_with_helpful_message()
        {
            var ex = Assert.Throws<KeyNotFoundException>(() => GpibProviders.Get("Nope"));
            Assert.Contains("Registered:", ex.Message);
        }

        [Fact]
        public void Register_then_resolve_custom_provider()
        {
            var custom = new SimulatedProviderAlias();
            GpibProviders.Register(custom);
            Assert.Same(custom, GpibProviders.Get("Custom-Alias"));
        }

        [Fact]
        public void Stub_providers_are_unavailable_with_a_reason()
        {
            foreach (var name in new[] { "Keysight-VISA", "Prologix", "AR488" })
            {
                var p = GpibProviders.Get(name);
                Assert.False(p.IsAvailable);
                Assert.False(string.IsNullOrWhiteSpace(p.UnavailableReason));
                Assert.ThrowsAny<Exception>(() => p.Open("whatever"));
            }
        }

        // A trivial custom provider used to prove registration works.
        private sealed class SimulatedProviderAlias : IGpibProvider
        {
            public string Name => "Custom-Alias";
            public ProviderCapabilities Capabilities { get; } =
                new ProviderCapabilities("Custom-Alias", false, false, false, false, false, false);
            public bool IsAvailable => false;
            public string UnavailableReason => "test alias";
            public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();
            public IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
                throw new NotImplementedException();
            public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
        }
    }
}
