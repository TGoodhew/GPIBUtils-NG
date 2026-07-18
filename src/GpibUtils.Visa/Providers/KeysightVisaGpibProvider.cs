using System;
using System.Collections.Generic;

namespace GpibUtils.Visa.Providers
{
    /// <summary>
    /// STUB — a placeholder for pinning specifically to Keysight's IO Libraries Suite (Keysight VISA)
    /// IVI.NET implementation.
    ///
    /// In most cases you do not need this: the default <c>NiVisaGpibProvider</c> is built on the
    /// vendor-neutral <c>Ivi.Visa.GlobalResourceManager</c>, so if Keysight VISA is your
    /// installed/primary system VISA it already drives your instruments through that provider. Implement
    /// this stub only if you must run Keysight VISA side-by-side with another VISA and select it
    /// explicitly (via Keysight's IVI.NET conflict resolution / preferred-implementation settings).
    ///
    /// See <c>docs/implementing-a-gpib-provider.md</c>.
    /// </summary>
    public sealed class KeysightVisaGpibProvider : IGpibProvider
    {
        public string Name => "Keysight-VISA";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "Keysight-VISA", discovery: true, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        public bool IsAvailable => false;

        public string UnavailableReason =>
            "Keysight-VISA provider is a stub. If Keysight VISA is your system VISA, use the 'NI-VISA' " +
            "(vendor-neutral VISA) provider instead. See docs/implementing-a-gpib-provider.md.";

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            throw new NotImplementedException(UnavailableReason);

        public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
    }
}
