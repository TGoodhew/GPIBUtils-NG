using System;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>
    /// Raised when an HP 53131A measurement does not complete — the operation-complete handshake timed out
    /// (typically no signal, or a signal below the counter's trigger level) or the instrument signalled an
    /// error during the measurement.
    /// </summary>
    public sealed class Hp53131AException : Exception
    {
        /// <summary>True when the failure was a completion timeout (no measurement produced) rather than an
        /// instrument-signalled error.</summary>
        public bool IsTimeout { get; }

        public Hp53131AException(string message, bool isTimeout) : base(message) => IsTimeout = isTimeout;

        internal static Hp53131AException Timeout(int channel, string detail) =>
            new Hp53131AException(
                $"channel {channel}: measurement did not complete — {detail}. Check the signal is present and above the trigger level.",
                isTimeout: true);

        internal static Hp53131AException InstrumentError(int channel, string detail) =>
            new Hp53131AException($"channel {channel}: instrument signalled an error during the measurement — {detail}.",
                isTimeout: false);
    }
}
