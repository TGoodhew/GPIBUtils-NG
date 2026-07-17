using System;

namespace GpibUtils.Instruments.Audio
{
    /// <summary>Raised when an HP 8903B measurement does not complete — the Special-Function-22 / data-ready
    /// SRQ handshake timed out, or the instrument signalled an error (status bit or an error-valued output).</summary>
    public sealed class Hp8903BException : Exception
    {
        /// <summary>True when the failure was a completion timeout rather than an instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp8903BException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp8903BException Timeout(string detail) =>
            new Hp8903BException($"measurement did not complete — {detail}. Check the instrument state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp8903BException InstrumentError(string detail) =>
            new Hp8903BException($"instrument signalled an error during the measurement — {detail}.", isTimeout: false);
    }
}
