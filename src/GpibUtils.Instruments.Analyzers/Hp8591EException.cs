using System;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Raised when an HP 8591E sweep does not complete — the RQS-mask / <c>STB?</c> completion handshake
    /// timed out, or the analyzer signalled an error (e.g. an illegal-command condition) during the sweep.
    /// </summary>
    public sealed class Hp8591EException : Exception
    {
        /// <summary>True when the failure was a completion timeout (no sweep completed) rather than an
        /// instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp8591EException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp8591EException Timeout(string detail) =>
            new Hp8591EException($"sweep did not complete — {detail}. Check the analyzer state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp8591EException InstrumentError(string detail) =>
            new Hp8591EException($"analyzer signalled an error during the sweep — {detail}.", isTimeout: false);
    }
}
