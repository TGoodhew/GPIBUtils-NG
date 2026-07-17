using System;

namespace GpibUtils.Instruments.LcrMeters
{
    /// <summary>Raised when an HP 4275A measurement does not complete — the Data-Ready SRQ handshake timed
    /// out, or the instrument signalled an error during the measurement.</summary>
    public sealed class Hp4275AException : Exception
    {
        /// <summary>True when the failure was a completion timeout rather than an instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp4275AException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp4275AException Timeout(string detail) =>
            new Hp4275AException($"measurement did not complete — {detail}. Check the instrument state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp4275AException InstrumentError(string detail) =>
            new Hp4275AException($"instrument signalled an error during the measurement — {detail}.", isTimeout: false);
    }
}
