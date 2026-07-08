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
        /// <summary>The decoded backend status, or <see cref="GpibStatus.Empty"/> if undecoded.</summary>
        public GpibStatus Status { get; }

        public GpibException(string message) : base(message) { }

        public GpibException(string message, Exception inner) : base(message, inner) { }

        public GpibException(string message, GpibStatus status, Exception inner = null)
            : base(message, inner)
        {
            Status = status;
        }
    }
}
