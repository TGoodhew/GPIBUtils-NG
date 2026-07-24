using System;

namespace GpibUtils.Visa
{
    /// <summary>
    /// Raised by a provider/session when a GPIB operation fails. Carries the decoded
    /// <see cref="GpibStatus"/> when the backend could describe the failure.
    /// </summary>
    [Serializable]
    public class GpibException : Exception
    {
        /// <summary>The decoded backend status, or <see cref="GpibStatus.Empty"/> if undecoded. When present
        /// it is folded into <see cref="Message"/> so it travels to every path that prints the message.</summary>
        public GpibStatus Status { get; }

        public GpibException(string message) : base(message) { }

        public GpibException(string message, Exception inner) : base(message, inner) { }

        public GpibException(string message, GpibStatus status, Exception inner = null)
            : base(message, inner)
        {
            Status = status;
        }

        /// <summary>
        /// The failure message with the decoded <see cref="Status"/> folded in (e.g. the VISA timeout /
        /// no-listener detail) when the backend described the failure, or the vendor
        /// <see cref="Exception.InnerException"/> message when the status is undecoded — so a bench operator
        /// sees the real cause wherever the message is printed, not just a generic "read failed".
        /// </summary>
        public override string Message
        {
            get
            {
                if (Status.HasName) return $"{base.Message} [{Status}]";
                if (InnerException != null &&
                    !string.Equals(InnerException.Message, base.Message, StringComparison.Ordinal))
                    return $"{base.Message} ({InnerException.Message})";
                return base.Message;
            }
        }
    }
}
