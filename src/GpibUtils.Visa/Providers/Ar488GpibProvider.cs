using System;
using System.Collections.Generic;

namespace GpibUtils.Visa.Providers
{
    /// <summary>
    /// STUB — AR488 Arduino-based GPIB controller.
    ///
    /// AR488 is an open-source controller that emulates the Prologix "++" command set over a USB serial
    /// (CDC) connection, so implement it much like <see cref="PrologixGpibProvider"/>: open the COM
    /// port, configure controller mode, and translate the <see cref="IInstrumentSession"/> operations to
    /// <c>++addr</c> / <c>++read</c> / <c>++spoll</c> / <c>++srq</c> / <c>++clr</c> / <c>++loc</c>.
    ///
    /// Differences to watch versus a genuine Prologix: serial latency and buffering (chunk large
    /// writes), firmware-version-dependent command support (gate optional commands on <c>++ver</c>), and
    /// default EOI/EOS handling. Parse a resource such as <c>"AR488::COM7::9"</c>.
    ///
    /// See <c>docs/implementing-a-gpib-provider.md</c>.
    /// </summary>
    public sealed class Ar488GpibProvider : IGpibProvider
    {
        public string Name => "AR488";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "AR488", discovery: false, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        public bool IsAvailable => false;

        public string UnavailableReason =>
            "AR488 provider is a stub. Implement it against the AR488 '++' serial command set. " +
            "See docs/implementing-a-gpib-provider.md.";

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            throw new NotImplementedException(UnavailableReason);

        public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
    }
}
