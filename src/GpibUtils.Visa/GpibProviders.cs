using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa.Providers;
using GpibUtils.Visa.Simulation;
// NI providers (GpibUtils.Visa.Ni) are loaded by reflection — no compile-time reference here.

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
            // Vendor-neutral built-ins that live in this dependency-free core assembly.
            Register(new KeysightVisaGpibProvider());
            Register(new PrologixGpibProvider());
            Register(new Ar488GpibProvider());
            Register(new SimulatedGpibProvider());

            // The NI providers live in the sibling GpibUtils.Visa.Ni assembly (which references the
            // official NI/IVI VISA.NET assemblies). Register them by reflection when that assembly is
            // deployed, so referencing GpibUtils.Visa.Ni is all it takes to get NI-VISA as the default —
            // while this core keeps no NI dependency and still builds/tests without it.
            TryAutoLoadExternal("GpibUtils.Visa.Ni.NiVisaGpibProvider, GpibUtils.Visa.Ni");
            TryAutoLoadExternal("GpibUtils.Visa.Ni.Ni4882GpibProvider, GpibUtils.Visa.Ni");
        }

        private static void TryAutoLoadExternal(string assemblyQualifiedTypeName)
        {
            try
            {
                var type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
                if (type != null && typeof(IGpibProvider).IsAssignableFrom(type))
                    Register((IGpibProvider)Activator.CreateInstance(type));
            }
            catch
            {
                // The external provider assembly (or its VISA dependencies) isn't present/loadable;
                // that provider simply stays unavailable. Not an error for a non-NI consumer.
            }
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
        public static IGpibProvider Default
        {
            get
            {
                var name = DefaultProviderName;
                if (TryGet(name, out var provider)) return provider;
                throw new InvalidOperationException(
                    $"Default provider '{name}' is not registered. Reference the GpibUtils.Visa.Ni " +
                    $"project to load the NI providers, or set GpibProviders.DefaultProviderName to one " +
                    $"of: {string.Join(", ", Names)}.");
            }
        }

        /// <summary>Opens a session on the default provider.</summary>
        public static IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            Default.Open(resourceName, settings);

        /// <summary>Opens a session on a named provider.</summary>
        public static IInstrumentSession Open(string providerName, string resourceName, SessionSettings settings) =>
            Get(providerName).Open(resourceName, settings);
    }
}
