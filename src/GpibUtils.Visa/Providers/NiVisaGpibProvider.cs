using System;
using System.Collections.Generic;
using System.Linq;
using Ivi.Visa;

namespace GpibUtils.Visa.Providers
{
    /// <summary>
    /// The default provider: VISA.NET message-based I/O (GPIB, USB-TMC, TCPIP/LXI, serial) through the
    /// vendor-neutral <see cref="GlobalResourceManager"/>. On a machine with NI-VISA installed this is
    /// NI-VISA; if Keysight VISA is the registered system VISA, the same code path uses that instead —
    /// which is what makes the suite portable across VISA vendors with no driver-specific code here.
    ///
    /// Native NI-488.2 board/primary/secondary addressing is a separate provider
    /// (<see cref="Ni4882GpibProvider"/>); this one addresses instruments by VISA resource string.
    /// </summary>
    public sealed class NiVisaGpibProvider : IGpibProvider
    {
        public string Name => "NI-VISA";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "NI-VISA", discovery: true, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        // The Ivi.Visa shared components restore from NuGet, so the managed API is always present; a
        // missing native VISA runtime surfaces as an exception on first Open/Discover, decoded via
        // DescribeError. We therefore report available and let real failures describe themselves.
        public bool IsAvailable => true;
        public string UnavailableReason => null;

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR")
        {
            try
            {
                return GlobalResourceManager.Find(string.IsNullOrEmpty(filter) ? "?*::INSTR" : filter).ToList();
            }
            catch
            {
                // VISA raises when nothing matches (or no runtime); treat as "none found".
                return Array.Empty<string>();
            }
        }

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Resource name must be provided.", nameof(resourceName));
            return new VisaInstrumentSession(this, resourceName, settings);
        }

        public GpibStatus DescribeError(Exception ex)
        {
            switch (ex)
            {
                case IOTimeoutException _:
                    return new GpibStatus("VI_ERROR_TMO", "Timeout expired before the operation completed.");
                case NativeVisaException nve:
                    return new GpibStatus("VISA_ERROR", nve.Message, nve.ErrorCode);
                case VisaException ve:
                    return new GpibStatus("VISA_ERROR", ve.Message);
                default:
                    return GpibStatus.Empty;
            }
        }
    }
}
