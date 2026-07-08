namespace GpibUtils.Visa
{
    /// <summary>
    /// What a GPIB provider can actually do, so higher-level code degrades or refuses cleanly rather
    /// than guessing. Not every adapter supports discovery, serial poll, SRQ, or native addressing
    /// (a Prologix/AR488 USB controller, for example, has a narrower surface than NI-VISA).
    /// </summary>
    public sealed class ProviderCapabilities
    {
        /// <summary>Human-readable provider name (e.g. "NI-VISA").</summary>
        public string Name { get; }
        /// <summary>Can enumerate connected resources (<see cref="IGpibProvider.Discover"/>).</summary>
        public bool Discovery { get; }
        /// <summary>Supports serial poll -&gt; status byte.</summary>
        public bool SerialPoll { get; }
        /// <summary>Supports waiting on a GPIB service request (SRQ).</summary>
        public bool ServiceRequest { get; }
        /// <summary>Supports the IEEE 488.2 device clear.</summary>
        public bool DeviceClear { get; }
        /// <summary>Supports returning an instrument to local control.</summary>
        public bool ReturnToLocal { get; }
        /// <summary>Supports native board/primary/secondary addressing (see <see cref="INativeGpib"/>).</summary>
        public bool NativeAddressing { get; }

        public ProviderCapabilities(string name, bool discovery, bool serialPoll, bool serviceRequest,
                                    bool deviceClear, bool returnToLocal, bool nativeAddressing)
        {
            Name = name;
            Discovery = discovery;
            SerialPoll = serialPoll;
            ServiceRequest = serviceRequest;
            DeviceClear = deviceClear;
            ReturnToLocal = returnToLocal;
            NativeAddressing = nativeAddressing;
        }
    }
}
