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
