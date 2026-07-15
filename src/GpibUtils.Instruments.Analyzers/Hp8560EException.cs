using System;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Raised when an HP 8560E sweep does not complete — the operation-complete SRQ handshake timed out, or
    /// the analyzer signalled an error (e.g. an UNCAL condition) during the sweep.
    /// </summary>
    public sealed class Hp8560EException : Exception
    {
        /// <summary>True when the failure was a completion timeout (no sweep completed) rather than an
        /// instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp8560EException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp8560EException Timeout(string detail) =>
            new Hp8560EException($"sweep did not complete — {detail}. Check the analyzer state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp8560EException InstrumentError(string detail) =>
            new Hp8560EException($"analyzer signalled an error during the sweep — {detail}.", isTimeout: false);
    }
}
