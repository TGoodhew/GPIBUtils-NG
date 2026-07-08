using System;
using System.Collections.Generic;

namespace GpibUtils.Visa.Providers
{
    /// <summary>
    /// STUB — Prologix GPIB-USB / GPIB-ETHERNET controller.
    ///
    /// A Prologix controller is driven as a serial/TCP stream using its "++" command set rather than
    /// VISA. To implement, open the COM port (or TCP socket) and translate the
    /// <see cref="IInstrumentSession"/> operations:
    /// <list type="bullet">
    ///   <item><description>Configure once: <c>++mode 1</c> (controller), <c>++auto 0</c>,
    ///     <c>++eoi 1</c>, <c>++eos 2</c> (append the instrument's terminator), <c>++read_tmo_ms N</c>.</description></item>
    ///   <item><description><c>Write(cmd)</c> → set <c>++addr &lt;pad&gt;</c> if changed, then send the
    ///     command line (escape the special bytes CR, LF, ESC, '+').</description></item>
    ///   <item><description><c>ReadString()</c> → send <c>++read eoi</c> and read one line back.</description></item>
    ///   <item><description><c>SerialPoll()</c> → <c>++spoll &lt;pad&gt;</c> returns the status byte.</description></item>
    ///   <item><description><c>WaitForServiceRequest()</c> → poll <c>++srq</c> (returns 0/1) until set or timeout.</description></item>
    ///   <item><description><c>Clear()</c> → <c>++clr</c>; <c>ReturnToLocal()</c> → <c>++loc</c>.</description></item>
    /// </list>
    /// Discovery is not supported by the hardware; addresses are configured, not enumerated.
    /// Parse the resource string yourself, e.g. <c>"PROLOGIX::COM5::9"</c> or <c>"PROLOGIX::192.168.1.20::9"</c>.
    ///
    /// See <c>docs/implementing-a-gpib-provider.md</c>.
    /// </summary>
    public sealed class PrologixGpibProvider : IGpibProvider
    {
        public string Name => "Prologix";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "Prologix", discovery: false, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: false);

        public bool IsAvailable => false;

        public string UnavailableReason =>
            "Prologix provider is a stub. Implement it against the '++' command set over the serial/TCP " +
            "stream. See docs/implementing-a-gpib-provider.md.";

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            throw new NotImplementedException(UnavailableReason);

        public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
    }
}
