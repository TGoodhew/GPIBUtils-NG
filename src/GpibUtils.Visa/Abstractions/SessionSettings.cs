namespace GpibUtils.Visa
{
    /// <summary>
    /// Per-session I/O options applied when a provider opens an <see cref="IInstrumentSession"/>.
    /// Defaults suit the great majority of GPIB instruments (newline-terminated ASCII, EOI on write).
    /// </summary>
    public sealed class SessionSettings
    {
        /// <summary>I/O timeout in milliseconds. Default 5000.</summary>
        public int TimeoutMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Read termination character. Default '\n'. Set to <c>null</c> to read to EOI / max-bytes only.
        /// </summary>
        public char? ReadTermination { get; set; } = '\n';

        /// <summary>Appended to <see cref="IInstrumentSession.Write(string)"/> commands. Default "\n".</summary>
        public string WriteTermination { get; set; } = "\n";

        /// <summary>Assert EOI on the last byte of a write. Default true.</summary>
        public bool AssertEndOnWrite { get; set; } = true;

        /// <summary>Returns a copy so a shared default is not mutated by a session.</summary>
        public SessionSettings Clone() => new SessionSettings
        {
            TimeoutMilliseconds = TimeoutMilliseconds,
            ReadTermination = ReadTermination,
            WriteTermination = WriteTermination,
            AssertEndOnWrite = AssertEndOnWrite
        };
    }
}
