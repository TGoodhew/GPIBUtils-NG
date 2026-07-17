using System;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>Raised when an HP 5005B measurement does not complete — the QM-mask / data-ready SRQ handshake
    /// timed out, or the instrument signalled an error during the measurement.</summary>
    public sealed class Hp5005BException : Exception
    {
        /// <summary>True when the failure was a completion timeout rather than an instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp5005BException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp5005BException Timeout(string detail) =>
            new Hp5005BException($"measurement did not complete — {detail}. Check the instrument state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp5005BException InstrumentError(string detail) =>
            new Hp5005BException($"instrument signalled an error during the measurement — {detail}.", isTimeout: false);
    }
}
