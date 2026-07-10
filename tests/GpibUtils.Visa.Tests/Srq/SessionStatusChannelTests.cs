using System;
using System.Collections.Generic;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;
using Xunit;

namespace GpibUtils.Visa.Tests.Srq
{
    /// <summary>Tests that <see cref="SessionStatusChannel"/> forwards to the underlying session.</summary>
    public class SessionStatusChannelTests
    {
        [Fact]
        public void Send_ForwardsToSessionWrite()
        {
            var session = new FakeSession { NextStatus = 0x00 };
            var channel = new SessionStatusChannel(session);

            channel.Send("RQS 48");
            channel.Send("SNGLS;TS;");

            Assert.Equal(new[] { "RQS 48", "SNGLS;TS;" }, session.Writes);
        }

        [Fact]
        public void SerialPoll_ReturnsSessionStatusByteValue()
        {
            var session = new FakeSession { NextStatus = 0x50 }; // commandComplete|... arbitrary
            var channel = new SessionStatusChannel(session);

            Assert.Equal(0x50, channel.SerialPoll());
            Assert.Equal(1, session.SerialPolls);
        }

        [Fact]
        public void NullSession_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SessionStatusChannel(null));
        }

        /// <summary>A stub session exposing only the two members the channel uses.</summary>
        private sealed class FakeSession : IInstrumentSession
        {
            public readonly List<string> Writes = new List<string>();
            public int SerialPolls;
            public byte NextStatus;

            public void Write(string command) => Writes.Add(command);
            public StatusByte SerialPoll() { SerialPolls++; return new StatusByte(NextStatus); }

            // Unused by SessionStatusChannel.
            public string ResourceName => "SIM::0::INSTR";
            public IGpibProvider Provider => throw new NotSupportedException();
            public int TimeoutMilliseconds { get; set; }
            public char? ReadTermination { get; set; }
            public string WriteTermination { get; set; } = string.Empty;
            public bool AssertEndOnWrite { get; set; }
            public void WriteBytes(byte[] data) => throw new NotSupportedException();
            public void WriteBytes(byte[] data, bool assertEnd) => throw new NotSupportedException();
            public byte[] ReadBytes(long maxBytes = 0) => throw new NotSupportedException();
            public string ReadString() => throw new NotSupportedException();
            public string Query(string command) => throw new NotSupportedException();
            public bool WaitForServiceRequest(int timeoutMs, out long elapsedMs) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void ReturnToLocal() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
