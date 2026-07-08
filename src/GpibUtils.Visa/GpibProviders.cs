using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa.Providers;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Visa
{
    /// <summary>
    /// The registry and factory for GPIB providers. Ships with NI-VISA (default), NI-488.2, the
    /// Keysight/Prologix/AR488 extension stubs, and an in-memory simulator; register your own with
    /// <see cref="Register"/>.
    ///
    /// The default provider is "NI-VISA" but can be overridden in code
    /// (<see cref="DefaultProviderName"/>) or via the <c>GPIBUTILS_GPIB_PROVIDER</c> environment
    /// variable.
    /// </summary>
    public static class GpibProviders
    {
        /// <summary>Environment variable that overrides the default provider name.</summary>
        public const string ProviderEnvVar = "GPIBUTILS_GPIB_PROVIDER";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, IGpibProvider> Registry =
            new Dictionary<string, IGpibProvider>(StringComparer.OrdinalIgnoreCase);
        private static string _defaultName = "NI-VISA";

        static GpibProviders()
        {
            RegisterBuiltIns();
        }

        private static void RegisterBuiltIns()
        {
            Register(new NiVisaGpibProvider());
            Register(new Ni4882GpibProvider());
            Register(new KeysightVisaGpibProvider());
            Register(new PrologixGpibProvider());
            Register(new Ar488GpibProvider());
            Register(new SimulatedGpibProvider());
        }

        /// <summary>Registers (or replaces, by name) a provider.</summary>
        public static void Register(IGpibProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(provider.Name))
                throw new ArgumentException("Provider.Name must be non-empty.", nameof(provider));
            lock (Gate) Registry[provider.Name] = provider;
        }

        /// <summary>All registered providers.</summary>
        public static IReadOnlyList<IGpibProvider> All
        {
            get { lock (Gate) return Registry.Values.ToList(); }
        }

        /// <summary>The registered names.</summary>
        public static IReadOnlyList<string> Names
        {
            get { lock (Gate) return Registry.Values.Select(p => p.Name).ToList(); }
        }

        /// <summary>Looks up a provider by name (case-insensitive). Throws if unknown.</summary>
        public static IGpibProvider Get(string name)
        {
            if (TryGet(name, out var provider)) return provider;
            throw new KeyNotFoundException(
                $"No GPIB provider named '{name}'. Registered: {string.Join(", ", Names)}.");
        }

        /// <summary>Looks up a provider by name (case-insensitive) without throwing.</summary>
        public static bool TryGet(string name, out IGpibProvider provider)
        {
            provider = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (Gate) return Registry.TryGetValue(name, out provider);
        }

        /// <summary>
        /// The default provider name. Reads the <c>GPIBUTILS_GPIB_PROVIDER</c> environment variable if
        /// set (and known); otherwise the in-code value (initially "NI-VISA").
        /// </summary>
        public static string DefaultProviderName
        {
            get
            {
                var env = Environment.GetEnvironmentVariable(ProviderEnvVar);
                if (!string.IsNullOrWhiteSpace(env) && TryGet(env, out _)) return env;
                return _defaultName;
            }
            set
            {
                if (!TryGet(value, out _))
                    throw new KeyNotFoundException($"Cannot set unknown default provider '{value}'.");
                _defaultName = value;
            }
        }

        /// <summary>The resolved default provider.</summary>
        public static IGpibProvider Default => Get(DefaultProviderName);

        /// <summary>Opens a session on the default provider.</summary>
        public static IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            Default.Open(resourceName, settings);

        /// <summary>Opens a session on a named provider.</summary>
        public static IInstrumentSession Open(string providerName, string resourceName, SessionSettings settings) =>
            Get(providerName).Open(resourceName, settings);
    }
}
