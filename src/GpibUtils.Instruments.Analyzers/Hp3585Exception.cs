using System;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Raised when an HP 3585 sweep does not complete — the operation-complete SRQ handshake timed out, or
    /// the analyzer signalled an error (e.g. a syntax error) during the sweep.
    /// </summary>
    public sealed class Hp3585Exception : Exception
    {
        /// <summary>True when the failure was a completion timeout rather than an instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp3585Exception(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp3585Exception Timeout(string detail) =>
            new Hp3585Exception($"sweep did not complete — {detail}. Check the analyzer state and HP-IB extender latency.",
                isTimeout: true);

        internal static Hp3585Exception InstrumentError(string detail) =>
            new Hp3585Exception($"analyzer signalled an error during the sweep — {detail}.", isTimeout: false);
    }
}
