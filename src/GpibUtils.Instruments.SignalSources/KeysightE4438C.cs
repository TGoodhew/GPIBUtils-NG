using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the Keysight / Agilent E4438C ESG vector RF signal generator (250 kHz – 6 GHz) — a SCPI
    /// instrument. Sets the CW carrier (frequency / power / RF+modulation on-off), queries the option-limited
    /// min/max ranges, and drives the Dual ARB player: download an interleaved 16-bit I/Q waveform to volatile
    /// memory (WFM1), select + play it, and copy to/from non-volatile storage. Ported from the
    /// <c>ESG-SignalCreator.Core/EsgController</c> (issue #11); the app's DSP waveform synthesis stays
    /// upstream — this driver takes ready 16-bit DAC samples. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion:</b> operations complete via <c>*OPC?</c> (the ESG has no armed-mask SRQ flow), so
    /// the #43 SRQ engine is not used; the ARB download blocks on <c>*OPC?</c> then reads
    /// <c>:SYSTem:ERRor?</c> so an instrument-side rejection (memory full, bad block) fails fast.</para>
    ///
    /// <para><b>ARB wire format:</b> samples are interleaved I,Q, 16-bit two's-complement, <b>big-endian</b>
    /// (MSB first), framed as one IEEE-488.2 definite-length block — the format the E4438C ARB requires.</para>
    /// </summary>
    public sealed class KeysightE4438C : ISignalSource
    {
        /// <summary>GPIB address of the ESG. The E4438C manual does not print a numeric factory-default
        /// HP-IB address; the legacy app used the lab value <b>19</b>. Override with <c>--address</c> or set
        /// the bench value via <c>config address set e4438c …</c>. Never trust bus-scan discovery on this
        /// bench (HP-IB extenders make every address look present).</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        /// <summary>Maximum ARB segment-name length the E4438C accepts.</summary>
        public const int MaxSegmentNameLength = 23;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public KeysightE4438C(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests). Binary ARB
        /// downloads are recorded as their SCPI prefix (e.g. <c>:MEMory:DATA "WFM1:seg",#…&lt;N bytes&gt;</c>).</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        private string Query(string command)
        {
            _history.Add(command);
            return (_session.Query(command) ?? string.Empty).Trim();
        }

        private static double ParseDouble(string raw, string what)
        {
            if (!double.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized E4438C {what} response: '{raw}'.");
            return v;
        }

        // ---- identity / state ----------------------------------------------

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            SetRfOutput(false);
        }

        public void Preset() => Send("*RST");

        public void Reset()
        {
            Send("*RST");
            Send("*CLS");
        }

        // ---- carrier -------------------------------------------------------

        /// <summary>Sets the CW carrier frequency in Hz (<c>:FREQuency:FIXed &lt;hz&gt; Hz</c>).</summary>
        public void SetFrequencyHz(double hertz) =>
            Send(":FREQuency:FIXed " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        /// <summary>Sets the CW carrier frequency in MHz (<see cref="ISignalSource"/> uniform surface).</summary>
        public void SetFrequencyMHz(double mhz) => SetFrequencyHz(mhz * 1e6);

        public double GetFrequencyHz() => ParseDouble(Query(":FREQuency:FIXed?"), "frequency");
        public double GetMinFrequencyHz() => ParseDouble(Query(":FREQuency:FIXed? MIN"), "min frequency");
        public double GetMaxFrequencyHz() => ParseDouble(Query(":FREQuency:FIXed? MAX"), "max frequency");

        /// <summary>Sets the RF output amplitude in dBm (<c>:POWer:LEVel &lt;dbm&gt; dBm</c>).</summary>
        public void SetPowerDbm(double dbm) =>
            Send(":POWer:LEVel " + dbm.ToString("G17", CultureInfo.InvariantCulture) + " dBm");

        public double GetPowerDbm() => ParseDouble(Query(":POWer:LEVel?"), "power");
        public double GetMinPowerDbm() => ParseDouble(Query(":POWer:LEVel? MIN"), "min power");
        public double GetMaxPowerDbm() => ParseDouble(Query(":POWer:LEVel? MAX"), "max power");

        /// <summary>Turns the RF output on or off (<c>:OUTPut:STATe</c>).</summary>
        public void SetRfOutput(bool on) => Send(":OUTPut:STATe " + (on ? "ON" : "OFF"));
        public void RfOn() => SetRfOutput(true);
        public void RfOff() => SetRfOutput(false);

        /// <summary>Enables or disables all modulation (<c>:OUTPut:MODulation:STATe</c>).</summary>
        public void SetModulation(bool on) => Send(":OUTPut:MODulation:STATe " + (on ? "ON" : "OFF"));

        // ---- reference -----------------------------------------------------

        /// <summary>Enables/disables automatic timebase selection (<c>:ROSCillator:SOURce:AUTO</c>): with auto
        /// on, the ESG locks to a valid external 10 MHz at REF IN and falls back to internal otherwise.</summary>
        public void SetReferenceAuto(bool on) =>
            Send(":ROSCillator:SOURce:AUTO " + (on ? "ON" : "OFF"));

        /// <summary>Reads which timebase the ESG is currently using (<c>:ROSCillator:SOURce?</c>), raw.</summary>
        public string GetReferenceSource() => Query(":ROSCillator:SOURce?");

        // ---- Dual ARB ------------------------------------------------------

        /// <summary>Enables or disables the arbitrary waveform generator (<c>:RADio:ARB:STATe</c>).</summary>
        public void SetArbState(bool on) => Send(":RADio:ARB:STATe " + (on ? "ON" : "OFF"));

        /// <summary>Selects a downloaded segment for the dual ARB player (<c>:RADio:ARB:WAVeform</c>).</summary>
        public void SelectWaveform(string segmentName)
        {
            ValidateSegmentName(segmentName);
            Send(":RADio:ARB:WAVeform \"WFM1:" + segmentName + "\"");
        }

        /// <summary>Sets the ARB sample (playback) clock in Hz (<c>:RADio:ARB:SCLock:RATE</c>, max 100 MHz).</summary>
        public void SetSampleClockHz(double hertz) =>
            Send(":RADio:ARB:SCLock:RATE " + hertz.ToString("G17", CultureInfo.InvariantCulture));

        public double GetMaxSampleClockHz() => ParseDouble(Query(":RADio:ARB:SCLock:RATE? MAX"), "max sample clock");

        /// <summary>Sets ARB waveform runtime scaling, in percent (<c>:RADio:ARB:RSCaling</c>).</summary>
        public void SetRuntimeScaling(double percent) =>
            Send(":RADio:ARB:RSCaling " + percent.ToString("0.###", CultureInfo.InvariantCulture));

        /// <summary>Selects a segment, sets the sample clock + runtime scaling, and turns the ARB on. RF
        /// output is controlled separately (see <see cref="SetRfOutput"/>).</summary>
        public void PlayWaveform(string segmentName, double sampleClockHz, double runtimeScalingPercent = 70)
        {
            SelectWaveform(segmentName);
            SetSampleClockHz(sampleClockHz);
            SetRuntimeScaling(runtimeScalingPercent);
            SetArbState(true);
        }

        /// <summary>
        /// Downloads an interleaved 16-bit I/Q waveform into volatile ARB memory (WFM1) via
        /// <c>:MEMory:DATA "WFM1:&lt;name&gt;",&lt;block&gt;</c>. <paramref name="i"/> and <paramref name="q"/>
        /// are equal-length arrays of ready 16-bit DAC codes (two's complement). The ARB is turned off first
        /// so a download never overwrites the segment currently playing; the transfer blocks on <c>*OPC?</c>
        /// and reads <c>:SYSTem:ERRor?</c> so an instrument-side rejection throws immediately.
        /// </summary>
        public void DownloadWaveform(string segmentName, short[] i, short[] q)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must be the same length.", nameof(q));
            if (i.Length == 0) throw new ArgumentException("Waveform is empty.", nameof(i));
            ValidateSegmentName(segmentName);

            SetArbState(false);   // never overwrite the playing segment
            byte[] payload = InterleaveBigEndian(i, q);
            byte[] message = Ieee4882Block(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"WFM1:{0}\",", segmentName), payload);
            _session.WriteBytes(message, true);
            _history.Add(string.Format(CultureInfo.InvariantCulture,
                ":MEMory:DATA \"WFM1:{0}\",#<{1} bytes>", segmentName, payload.Length));

            // Completion + error read-back.
            Query("*OPC?");
            string err = Query(":SYSTem:ERRor?");
            if (!IsNoError(err))
                throw new InvalidOperationException(
                    $"The E4438C rejected the waveform download for '{segmentName}': {err}");
        }

        /// <summary>Downloads a marker stream (one byte per sample, 1 = marker on) to the segment's marker
        /// file (<c>:MEMory:DATA "MKR1:&lt;name&gt;"</c>). Optional — the ESG supplies defaults otherwise.</summary>
        public void DownloadMarkers(string segmentName, byte[] markers)
        {
            if (markers == null) throw new ArgumentNullException(nameof(markers));
            ValidateSegmentName(segmentName);
            byte[] message = Ieee4882Block(
                string.Format(CultureInfo.InvariantCulture, ":MEMory:DATA \"MKR1:{0}\",", segmentName), markers);
            _session.WriteBytes(message, true);
            _history.Add(string.Format(CultureInfo.InvariantCulture,
                ":MEMory:DATA \"MKR1:{0}\",#<{1} bytes>", segmentName, markers.Length));
        }

        /// <summary>Copies a volatile WFM1 segment into non-volatile ARB storage (<c>NVWFM</c>).</summary>
        public void CopyToNonVolatile(string segmentName)
        {
            ValidateSegmentName(segmentName);
            Send(string.Format(CultureInfo.InvariantCulture, ":MEMory:COPY \"WFM1:{0}\",\"NVWFM:{0}\"", segmentName));
        }

        /// <summary>Loads a non-volatile NVWFM segment back into volatile WFM1 memory for playback.</summary>
        public void LoadFromNonVolatile(string segmentName)
        {
            ValidateSegmentName(segmentName);
            Send(string.Format(CultureInfo.InvariantCulture, ":MEMory:COPY \"NVWFM:{0}\",\"WFM1:{0}\"", segmentName));
        }

        /// <summary>Reads the head of the SCPI error queue (<c>:SYSTem:ERRor?</c>).</summary>
        public string GetError() => Query(":SYSTem:ERRor?");

        // ---- encoding helpers ----------------------------------------------

        /// <summary>Interleaves I,Q into 16-bit two's-complement, big-endian (MSB first) bytes.</summary>
        internal static byte[] InterleaveBigEndian(short[] i, short[] q)
        {
            var bytes = new byte[i.Length * 4];
            int p = 0;
            for (int n = 0; n < i.Length; n++)
            {
                bytes[p++] = (byte)((i[n] >> 8) & 0xFF);
                bytes[p++] = (byte)(i[n] & 0xFF);
                bytes[p++] = (byte)((q[n] >> 8) & 0xFF);
                bytes[p++] = (byte)(q[n] & 0xFF);
            }
            return bytes;
        }

        /// <summary>Frames <paramref name="payload"/> as an IEEE-488.2 definite-length block after an ASCII
        /// <paramref name="prefix"/> — <c>&lt;prefix&gt;#&lt;ndigits&gt;&lt;length&gt;&lt;payload&gt;</c>.</summary>
        internal static byte[] Ieee4882Block(string prefix, byte[] payload)
        {
            string length = payload.Length.ToString(CultureInfo.InvariantCulture);
            string header = prefix + "#" + length.Length.ToString(CultureInfo.InvariantCulture) + length;
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            var message = new byte[headerBytes.Length + payload.Length];
            Buffer.BlockCopy(headerBytes, 0, message, 0, headerBytes.Length);
            Buffer.BlockCopy(payload, 0, message, headerBytes.Length, payload.Length);
            return message;
        }

        /// <summary>True if a <c>:SYSTem:ERRor?</c> reply reports no error (code 0), or is empty.</summary>
        internal static bool IsNoError(string errorReply)
        {
            if (string.IsNullOrWhiteSpace(errorReply)) return true;
            string code = errorReply.Split(',')[0].Trim();
            return code == "0" || code == "+0" || code == "-0";
        }

        private static void ValidateSegmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A waveform segment name is required.", nameof(name));
            if (name.Length > MaxSegmentNameLength)
                throw new ArgumentException($"Segment name must be {MaxSegmentNameLength} characters or fewer.", nameof(name));
        }
    }
}
