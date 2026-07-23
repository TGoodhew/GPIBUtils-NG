using System;
using GpibUtils.Visa;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// A flattened, console-independent view of one <see cref="IGpibProvider"/> for the Providers screen:
    /// its name, availability, whether it is the registry default, and its capability flags. Pure data so
    /// the mapping is unit-testable; the screen turns a list of these into a Spectre table.
    /// </summary>
    public sealed class ProviderCapabilityRow
    {
        public string Name { get; private set; }
        public bool IsAvailable { get; private set; }
        public bool IsDefault { get; private set; }
        public string UnavailableReason { get; private set; }

        public bool Discovery { get; private set; }
        public bool SerialPoll { get; private set; }
        public bool ServiceRequest { get; private set; }
        public bool DeviceClear { get; private set; }
        public bool ReturnToLocal { get; private set; }
        public bool NativeAddressing { get; private set; }

        /// <summary>Flattens <paramref name="provider"/>, marking it default when its name matches
        /// <paramref name="defaultProviderName"/> (compared case-insensitively).</summary>
        public static ProviderCapabilityRow From(IGpibProvider provider, string defaultProviderName)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var c = provider.Capabilities;
            return new ProviderCapabilityRow
            {
                Name = provider.Name,
                IsAvailable = provider.IsAvailable,
                IsDefault = string.Equals(provider.Name, defaultProviderName, StringComparison.OrdinalIgnoreCase),
                UnavailableReason = provider.UnavailableReason,
                Discovery = c.Discovery,
                SerialPoll = c.SerialPoll,
                ServiceRequest = c.ServiceRequest,
                DeviceClear = c.DeviceClear,
                ReturnToLocal = c.ReturnToLocal,
                NativeAddressing = c.NativeAddressing,
            };
        }
    }
}
