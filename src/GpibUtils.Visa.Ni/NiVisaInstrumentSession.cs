#if NI_VISA
using System;
using System.Diagnostics;
using System.Text;
using Ivi.Visa;
using NationalInstruments.Visa;

namespace GpibUtils.Visa.Ni
{
    /// <summary>
    /// An <see cref="IInstrumentSession"/> backed by NI-VISA.NET (<see cref="MessageBasedSession"/>),
    /// using the official National Instruments VISA.NET assemblies.
    /// </summary>
    internal sealed class NiVisaInstrumentSession : IInstrumentSession
    {
        /// <summary>1:1 byte-to-char encoding (Latin-1) for lossless string/byte conversion.</summary>
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        private readonly MessageBasedSession _session;
        private char? _readTermination;

        public NiVisaInstrumentSession(IGpibProvider provider, ResourceManager resourceManager,
                                       string resourceName, SessionSettings settings)
        {
            Provider = provider;
            ResourceName = resourceName;
            settings = settings ?? new SessionSettings();

            try
            {
                _session = (MessageBasedSession)resourceManager.Open(resourceName);
            }
            catch (Exception ex)
            {
                throw new GpibException(
                    $"Failed to open VISA resource '{resourceName}'.", provider.DescribeError(ex), ex);
            }

            TimeoutMilliseconds = settings.TimeoutMilliseconds;
            WriteTermination = settings.WriteTermination ?? string.Empty;
            AssertEndOnWrite = settings.AssertEndOnWrite;
            ReadTermination = settings.ReadTermination;
        }

        public string ResourceName { get; }
        public IGpibProvider Provider { get; }

        public int TimeoutMilliseconds
        {
            get => _session.TimeoutMilliseconds;
            set => _session.TimeoutMilliseconds = value;
        }

        public string WriteTermination { get; set; }
        public bool AssertEndOnWrite { get; set; }

        public char? ReadTermination
        {
            get => _readTermination;
            set
            {
                _readTermination = value;
                if (value.HasValue)
                {
                    _session.TerminationCharacter = (byte)value.Value;
                    _session.TerminationCharacterEnabled = true;
                }
                else
                {
                    _session.TerminationCharacterEnabled = false;
                }
            }
        }

        public void WriteBytes(byte[] data) => WriteBytes(data, AssertEndOnWrite);

        public void WriteBytes(byte[] data, bool assertEnd)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _session.SendEndEnabled = assertEnd;
            try { _session.RawIO.Write(data); }
            catch (Exception ex) { throw Wrap("write", ex); }
            finally { if (!assertEnd) _session.SendEndEnabled = true; }
        }

        public void Write(string command)
        {
            var text = command + (WriteTermination ?? string.Empty);
            WriteBytes(Latin1.GetBytes(text), AssertEndOnWrite);
        }

        public byte[] ReadBytes(long maxBytes = 0)
        {
            try
            {
                if (maxBytes > 0) return _session.RawIO.Read(maxBytes);
                return Latin1.GetBytes(_session.RawIO.ReadString());
            }
            catch (Exception ex) { throw Wrap("read", ex); }
        }

        public string ReadString()
        {
            try { return _session.RawIO.ReadString(); }
            catch (Exception ex) { throw Wrap("read", ex); }
        }

        public string Query(string command)
        {
            Write(command);
            return ReadString();
        }

        public StatusByte SerialPoll()
        {
            try { return new StatusByte((byte)_session.ReadStatusByte()); }
            catch (Exception ex) { throw Wrap("serial poll", ex); }
        }

        public bool WaitForServiceRequest(int timeoutMs, out long elapsedMs)
        {
            var watch = Stopwatch.StartNew();
            _session.EnableEvent(EventType.ServiceRequest);
            try
            {
                _session.WaitOnEvent(EventType.ServiceRequest, timeoutMs);
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                return true;
            }
            catch (IOTimeoutException)
            {
                watch.Stop();
                elapsedMs = watch.ElapsedMilliseconds;
                return false;
            }
            finally
            {
                try { _session.DisableEvent(EventType.ServiceRequest); }
                catch { /* best effort */ }
            }
        }

        public void Clear()
        {
            try { _session.Clear(); }
            catch (Exception ex) { throw Wrap("device clear", ex); }
        }

        public void ReturnToLocal()
        {
            try
            {
                if (_session is GpibSession gpib)
                    gpib.SendRemoteLocalCommand(GpibInstrumentRemoteLocalMode.GoToLocal);
            }
            catch { /* not fatal — some links/instruments do not support REN control */ }
        }

        private GpibException Wrap(string op, Exception ex) =>
            new GpibException($"NI-VISA {op} failed on '{ResourceName}'.", Provider.DescribeError(ex), ex);

        public void Dispose()
        {
            try { _session?.Dispose(); }
            catch { /* best effort */ }
        }
    }
}
#endif
