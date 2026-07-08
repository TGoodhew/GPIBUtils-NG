using System;
using System.Collections.Generic;

namespace GpibUtils.Visa
{
    /// <summary>
    /// A pluggable GPIB backend. Implement this (plus <see cref="IInstrumentSession"/>) to add support
    /// for a new controller — Prologix, AR488, a specific vendor's VISA, a raw board driver — without
    /// touching any instrument driver. Register the implementation with
    /// <see cref="GpibProviders.Register"/> and it becomes selectable by name.
    ///
    /// See <c>docs/implementing-a-gpib-provider.md</c> for a full authoring walkthrough.
    /// </summary>
    public interface IGpibProvider
    {
        /// <summary>Stable, human-readable name used to select the provider (e.g. "NI-VISA", "Prologix").</summary>
        string Name { get; }

        /// <summary>What this provider supports, so callers can degrade or refuse cleanly.</summary>
        ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// True when the provider's runtime dependencies are present and it can open sessions on this
        /// machine. A stub or an adapter whose driver/hardware is missing returns false and explains
        /// via <see cref="UnavailableReason"/>.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>Why the provider is unavailable (null/empty when <see cref="IsAvailable"/> is true).</summary>
        string UnavailableReason { get; }

        /// <summary>
        /// Discovers connected resources matching <paramref name="filter"/>. Returns an empty list when
        /// discovery is unsupported or nothing matches. Never throws for "no instruments".
        /// </summary>
        IReadOnlyList<string> Discover(string filter = "?*::INSTR");

        /// <summary>Opens a session to <paramref name="resourceName"/>, applying <paramref name="settings"/>
        /// (or provider defaults when null).</summary>
        IInstrumentSession Open(string resourceName, SessionSettings settings = null);

        /// <summary>Decodes a backend exception to a <see cref="GpibStatus"/> (Empty if it cannot).</summary>
        GpibStatus DescribeError(Exception ex);
    }

    /// <summary>
    /// Optional capability for providers that can address a device by board/primary/secondary directly
    /// (NI-488.2 style), bypassing a resource string. Providers that support it advertise
    /// <see cref="ProviderCapabilities.NativeAddressing"/> and implement this on the same object.
    /// </summary>
    public interface INativeGpib
    {
        /// <summary>Opens a transient device handle at board/primary/secondary, writes, and reads the response.</summary>
        string NativeQuery(int board, byte primaryAddress, byte secondaryAddress, string command);
    }
}
