using System;

namespace GpibUtils.Visa
{
    /// <summary>
    /// A live connection to one instrument, returned by <see cref="IGpibProvider.Open"/>. This is the
    /// surface instrument drivers program against — deliberately provider-agnostic, so the same driver
    /// runs over NI-VISA, Keysight VISA, a Prologix/AR488 adapter, or the in-memory simulator.
    ///
    /// Instances are not required to be thread-safe; a driver should serialize access to a session.
    /// </summary>
    public interface IInstrumentSession : IDisposable
    {
        /// <summary>The resource string / address this session was opened for.</summary>
        string ResourceName { get; }

        /// <summary>The provider that created this session.</summary>
        IGpibProvider Provider { get; }

        /// <summary>I/O timeout in milliseconds.</summary>
        int TimeoutMilliseconds { get; set; }

        /// <summary>Read termination character; <c>null</c> reads to EOI / max-bytes only.</summary>
        char? ReadTermination { get; set; }

        /// <summary>String appended by <see cref="Write(string)"/>.</summary>
        string WriteTermination { get; set; }

        /// <summary>Assert EOI on the last byte of a write.</summary>
        bool AssertEndOnWrite { get; set; }

        /// <summary>Writes raw bytes (EOI on the last byte per <see cref="AssertEndOnWrite"/>).</summary>
        void WriteBytes(byte[] data);

        /// <summary>Writes raw bytes, explicitly controlling whether EOI is asserted on the final byte.
        /// Pass <paramref name="assertEnd"/>=false for intermediate chunks of a streamed message.</summary>
        void WriteBytes(byte[] data, bool assertEnd);

        /// <summary>Writes a command string followed by <see cref="WriteTermination"/>.</summary>
        void Write(string command);

        /// <summary>Reads raw bytes. <paramref name="maxBytes"/>=0 reads to termination/EOI.</summary>
        byte[] ReadBytes(long maxBytes = 0);

        /// <summary>Reads a response and decodes it as text (to termination/EOI).</summary>
        string ReadString();

        /// <summary>Convenience: <see cref="Write(string)"/> then <see cref="ReadString"/>.</summary>
        string Query(string command);

        /// <summary>Serial-polls the instrument and returns its status byte.</summary>
        StatusByte SerialPoll();

        /// <summary>Waits for the instrument to assert SRQ. Returns true if asserted before the timeout.</summary>
        bool WaitForServiceRequest(int timeoutMs, out long elapsedMs);

        /// <summary>Sends the IEEE 488.2 device clear.</summary>
        void Clear();

        /// <summary>Returns the instrument to local (front-panel) control. No-op if unsupported.</summary>
        void ReturnToLocal();
    }
}
