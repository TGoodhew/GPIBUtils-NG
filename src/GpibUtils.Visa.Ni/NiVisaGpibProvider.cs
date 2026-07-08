using System;
using System.Collections.Generic;
using System.Linq;
using Ivi.Visa;
using NationalInstruments.Visa;

namespace GpibUtils.Visa.Ni
{
    /// <summary>
    /// The default provider: NI-VISA.NET message-based I/O (GPIB, USB-TMC, TCPIP/LXI, serial) using the
    /// official National Instruments VISA.NET assemblies. Instruments are addressed by VISA resource
    /// string; native NI-488.2 board/primary/secondary addressing is <see cref="Ni4882GpibProvider"/>.
    /// </summary>
    public sealed class NiVisaGpibProvider : IGpibProvider
    {
        private readonly object _gate = new object();
        private ResourceManager _resourceManager;

        public string Name => "NI-VISA";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "NI-VISA", discovery: true, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        public bool IsAvailable => true;
        public string UnavailableReason => null;

        // Created lazily so merely registering the provider never touches the native VISA runtime.
        private ResourceManager Rm
        {
            get { lock (_gate) return _resourceManager ?? (_resourceManager = new ResourceManager()); }
        }

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR")
        {
            try
            {
                return Rm.Find(string.IsNullOrEmpty(filter) ? "?*::INSTR" : filter).ToList();
            }
            catch
            {
                // NI-VISA raises when nothing matches (or no runtime); treat as "none found".
                return Array.Empty<string>();
            }
        }

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Resource name must be provided.", nameof(resourceName));
            return new NiVisaInstrumentSession(this, Rm, resourceName, settings);
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
