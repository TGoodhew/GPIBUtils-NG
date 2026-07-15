using System;

namespace GpibUtils.Visa.Simulation
{
    /// <summary>
    /// A scriptable fake instrument used by <see cref="SimulatedGpibProvider"/> so drivers and the whole
    /// suite build and test with no hardware. Configure a <see cref="Responder"/> for instrument-specific
    /// behaviour, or rely on the built-in handling of the common IEEE 488.2 common commands.
    /// </summary>
    public sealed class SimulatedInstrument
    {
        /// <summary>Reply to <c>*IDN?</c>.</summary>
        public string IdentificationString { get; set; } = "GPIBUtils,Simulated Instrument,0,1.0";

        /// <summary>
        /// Optional custom responder: given a query command (terminators stripped), return the reply.
        /// Return <c>null</c> to fall back to the default handling. Non-query writes never call this.
        /// </summary>
        public Func<string, string> Responder { get; set; }

        /// <summary>
        /// Optional observer invoked for every write (command or query, terminators stripped). Lets a
        /// simulated <b>listen-only</b> instrument (e.g. the HP 11713A, which cannot be queried) track the
        /// state it is being driven into, so drivers for such devices verify without hardware.
        /// </summary>
        public Action<string> WriteObserver { get; set; }

        /// <summary>Notifies <see cref="WriteObserver"/> of a write. Called by the simulated session.</summary>
        internal void ObserveWrite(string command) => WriteObserver?.Invoke(command);

        /// <summary>
        /// Optional hook invoked immediately before every serial poll reads the status byte. Lets a model
        /// advance a multi-poll state machine — e.g. an SRQ-edge sweep that must go BUSY (the expect bit
        /// clears) on one poll and assert request-service (RQS) on a later one — so the #43
        /// <see cref="Srq.CompletionWaiter"/> SRQ-edge flow can be exercised headlessly (used by the HP 8560E).
        /// </summary>
        public Action OnSerialPoll { get; set; }

        /// <summary>Advances the model's per-poll state. Called by the simulated session before each poll.</summary>
        internal void ObserveSerialPoll() => OnSerialPoll?.Invoke();

        /// <summary>The status byte returned by a serial poll (RQS/bit-6 is OR-ed in when
        /// <see cref="ServiceRequestPending"/> is set).</summary>
        public byte StatusByte { get; set; }

        /// <summary>When true, serial poll sets RQS and <see cref="IInstrumentSession.WaitForServiceRequest"/>
        /// returns immediately.</summary>
        public bool ServiceRequestPending { get; set; }

        /// <summary>Default reply logic for a query when no <see cref="Responder"/> handles it.</summary>
        internal string DefaultRespond(string command)
        {
            switch (command.Trim().ToUpperInvariant())
            {
                case "*IDN?": return IdentificationString;
                case "*OPC?": return "1";
                case "*TST?": return "0";
                case "*ESR?": return "0";
                case "*STB?": return ((int)EffectiveStatusByte()).ToString();
                default: return command.TrimEnd().EndsWith("?") ? "0" : string.Empty;
            }
        }

        internal byte EffectiveStatusByte() =>
            (byte)(StatusByte | (ServiceRequestPending ? 0x40 : 0x00));
    }
}
