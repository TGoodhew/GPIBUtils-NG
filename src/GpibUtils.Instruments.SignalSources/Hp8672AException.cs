using System;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>Raised when the HP 8672A does not re-acquire phase lock within the settle timeout after a
    /// frequency change.</summary>
    public sealed class Hp8672AException : Exception
    {
        public Hp8672AException(string message) : base(message) { }
    }
}
