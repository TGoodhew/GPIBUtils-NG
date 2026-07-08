using System;
using System.Collections.Generic;

namespace GpibUtils.Visa.Ni
{
    /// <summary>
    /// Native NI-488.2 provider: addresses devices directly by board / primary / secondary address via
    /// the official National Instruments GPIB driver (<see cref="INativeGpib"/>), bypassing VISA
    /// resource strings. For ordinary message-based sessions the <see cref="NiVisaGpibProvider"/> sits
    /// on the same NI driver and is preferred.
    ///
    /// <c>NationalInstruments.NI4882</c> ships with the NI-488.2 driver (not on NuGet), so this
    /// provider's live code compiles only when the <c>NI4882</c> constant is defined
    /// (<c>dotnet build -p:DefineConstants=NI4882</c>) with the driver installed. Without it the
    /// provider reports unavailable and its operations throw with guidance.
    /// </summary>
    public sealed class Ni4882GpibProvider : IGpibProvider, INativeGpib
    {
        public string Name => "NI-488.2";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities(
            name: "NI-488.2", discovery: false, serialPoll: true, serviceRequest: true,
            deviceClear: true, returnToLocal: true, nativeAddressing: true);

#if NI4882
        public bool IsAvailable => true;
        public string UnavailableReason => null;

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null)
        {
            // Native 488.2 addresses by board/primary/secondary, not by VISA resource string. For a
            // persistent message-based session use the NI-VISA provider (same NI driver); use
            // NativeQuery here for one-shot native-addressed exchanges.
            throw new NotSupportedException(
                "The NI-488.2 provider exposes one-shot native addressing via NativeQuery. For a " +
                "persistent message-based session, open the resource through the 'NI-VISA' provider.");
        }

        public string NativeQuery(int board, byte primaryAddress, byte secondaryAddress, string command)
        {
            var payload = command.EndsWith("\n") ? command : command + "\n";
            using (var device = new NationalInstruments.NI4882.Device(board, primaryAddress, secondaryAddress))
            {
                device.Write(payload);
                return device.ReadString();
            }
        }
#else
        public bool IsAvailable => false;

        public string UnavailableReason =>
            "NI-488.2 support is not compiled in. Build GpibUtils.Visa.Ni with the NI4882 constant " +
            "(dotnet build -p:DefineConstants=NI4882) on a machine with the NI-488.2 driver installed.";

        public IReadOnlyList<string> Discover(string filter = "?*::INSTR") => Array.Empty<string>();

        public IInstrumentSession Open(string resourceName, SessionSettings settings = null) =>
            throw new NotSupportedException(UnavailableReason);

        public string NativeQuery(int board, byte primaryAddress, byte secondaryAddress, string command) =>
            throw new NotSupportedException(UnavailableReason);
#endif

        public GpibStatus DescribeError(Exception ex) => GpibStatus.Empty;
    }
}
