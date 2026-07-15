using System;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Raised when an HP 85620A mass-memory operation is rejected — the analyzer's <c>ERR?;</c> query
    /// returned a non-zero error code after a store/load/define (e.g. a full card, a name clash, or a
    /// missing file).
    /// </summary>
    public sealed class Hp85620AException : Exception
    {
        /// <summary>The analyzer-side error code (<c>ERR?;</c>) that was non-zero, or 0 if not applicable.</summary>
        public int ErrorCode { get; }

        public Hp85620AException(string message, int errorCode) : base(message) => ErrorCode = errorCode;

        internal static Hp85620AException FromErr(string operation, string name, int err) =>
            new Hp85620AException($"{operation} '{name}' failed — analyzer ERR? returned {err}.", err);
    }
}
