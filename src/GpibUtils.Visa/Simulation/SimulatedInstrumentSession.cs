using System;
using System.Text;

namespace GpibUtils.Visa.Simulation
{
    /// <summary>
    /// An <see cref="IInstrumentSession"/> over a <see cref="SimulatedInstrument"/>. A write records the
    /// last command (applying the common-command side effects); a read replays the response the
    /// instrument would give to that command.
    /// </summary>
    internal sealed class SimulatedInstrumentSession : IInstrumentSession
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        private readonly SimulatedInstrument _instrument;
        private string _lastCommand = string.Empty;
        private bool _disposed;

        public SimulatedInstrumentSession(IGpibProvider provider, string resourceName,
                                          SimulatedInstrument instrument, SessionSettings settings)
        {
            Provider = provider;
            ResourceName = resourceName;
            _instrument = instrument;
            settings = settings ?? new SessionSettings();
            TimeoutMilliseconds = settings.TimeoutMilliseconds;
            ReadTermination = settings.ReadTermination;
            WriteTermination = settings.WriteTermination ?? string.Empty;
            AssertEndOnWrite = settings.AssertEndOnWrite;
        }

        public string ResourceName { get; }
        public IGpibProvider Provider { get; }
        public int TimeoutMilliseconds { get; set; }
        public char? ReadTermination { get; set; }
        public string WriteTermination { get; set; }
        public bool AssertEndOnWrite { get; set; }

        public void WriteBytes(byte[] data) => WriteBytes(data, AssertEndOnWrite);

        public void WriteBytes(byte[] data, bool assertEnd)
        {
            ThrowIfDisposed();
            if (data == null) throw new ArgumentNullException(nameof(data));
            ApplyCommand(Latin1.GetString(data));
        }

        public void Write(string command)
        {
            ThrowIfDisposed();
            ApplyCommand(command);
        }

        private void ApplyCommand(string raw)
        {
            var command = raw.TrimEnd('\r', '\n');
            _lastCommand = command;

            // Side effects for the common commands that carry them.
            switch (command.Trim().ToUpperInvariant())
            {
                case "*CLS":
                    _instrument.StatusByte = 0;
                    _instrument.ServiceRequestPending = false;
                    break;
                case "*RST":
                    // A reset clears transient state but keeps identity.
                    _instrument.ServiceRequestPending = false;
                    break;
            }
        }

        private string Respond()
        {
            var command = _lastCommand;
            var custom = _instrument.Responder?.Invoke(command);
            return custom ?? _instrument.DefaultRespond(command);
        }

        public byte[] ReadBytes(long maxBytes = 0)
        {
            ThrowIfDisposed();
            var bytes = Latin1.GetBytes(Respond());
            if (maxBytes > 0 && bytes.Length > maxBytes)
            {
                var trimmed = new byte[maxBytes];
                Array.Copy(bytes, trimmed, maxBytes);
                return trimmed;
            }
            return bytes;
        }

        public string ReadString()
        {
            ThrowIfDisposed();
            return Respond();
        }

        public string Query(string command)
        {
            Write(command);
            return ReadString();
        }

        public StatusByte SerialPoll()
        {
            ThrowIfDisposed();
            return new StatusByte(_instrument.EffectiveStatusByte());
        }

        public bool WaitForServiceRequest(int timeoutMs, out long elapsedMs)
        {
            ThrowIfDisposed();
            if (_instrument.ServiceRequestPending) { elapsedMs = 0; return true; }
            elapsedMs = timeoutMs;
            return false;
        }

        public void Clear()
        {
            ThrowIfDisposed();
            _lastCommand = string.Empty;
            _instrument.StatusByte = 0;
            _instrument.ServiceRequestPending = false;
        }

        public void ReturnToLocal() => ThrowIfDisposed();

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SimulatedInstrumentSession));
        }

        public void Dispose() => _disposed = true;
    }
}
