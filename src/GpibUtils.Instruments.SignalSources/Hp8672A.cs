using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using GpibUtils.Visa;
using GpibUtils.Visa.Srq;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// Driver for the HP 8672A Synthesized Microwave Signal Generator (2–18 GHz), the older sibling of the
    /// already-migrated 8673B. It speaks the pre-488.2 weighted "program-code letter + argument + EXECUTE"
    /// HP-IB language (no <c>*IDN?</c>/<c>*OPC</c>/<c>*SRE</c>), NOT the 8673B's friendlier mnemonics — so this
    /// is a distinct driver sharing only the <see cref="ISignalSource"/> surface. Reconstructed from the 8672A
    /// Operating &amp; Service manual (issue #126); runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Command letters — bench-confirm.</b> The frequency form <c>P&lt;kHz&gt;Z</c> (execute) is
    /// legible in the manual and reliable. The RANGE / VERNIER / ALC-output code letters and their argument
    /// encodings come from a manual table whose OCR was garbled, so <see cref="RangeCode"/>/
    /// <see cref="VernierCode"/>/<see cref="OutputCode"/> and the power/RF encodings below are a best-effort
    /// reconstruction flagged <c>TBD</c>: confirm against the manual pages or the bench unit before trusting
    /// them for level/RF control.</para>
    ///
    /// <para><b>Phase-lock settle — an #96 <see cref="StatusOperation.ExpectBitCleared"/> consumer.</b> The
    /// 8672A has no armable operation-complete flag and no enable mask; instead, after a frequency change the
    /// "not phase-locked" status bit (weight 8) is asserted until the synthesizer re-acquires lock, then
    /// clears. <see cref="WaitForPhaseLock"/> drives the shared <see cref="CompletionWaiter"/> direct-bit flow
    /// with <c>ExpectBitCleared</c> and no enable mask (status read by hardware serial poll). Timeouts stay
    /// generous for HP-IB bus-extender latency.</para>
    /// </summary>
    public sealed class Hp8672A : ISignalSource
    {
        /// <summary>GPIB address of the 8672A — manual factory default is octal 23 = decimal 19. NOTE: the
        /// 8673B and 8340B also default to 19; expect a bench remap to avoid a clash on a shared bus segment.
        /// Override with <c>--address</c>. Never trust bus-scan discovery behind HP-IB extenders.</summary>
        public const string DefaultResource = "GPIB0::19::INSTR";

        // Command letters. 'P'/'Z' (frequency/execute) are reliable; the rest are reconstructed — CONFIRM (#126).
        private const char FrequencyCode = 'P', ExecuteCode = 'Z';
        private const char RangeCode = 'K', VernierCode = 'L', OutputCode = 'O';   // TBD: confirm at bench

        /// <summary>Status-byte bit weights (manual "Sending the Status Byte Message", serial-poll response).</summary>
        private const int OverrangePlus10Bit = 1, FmOvermodBit = 2, LevelUncalBit = 4, NotPhaseLockedBit = 8,
                          RfOffBit = 16, FreqOutOfRangeBit = 32, RequestServiceBit = 64, OvenColdBit = 128;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Backstop for the phase-lock settle wait, ms. Generous for a wide microwave retune plus
        /// HP-IB extender turnaround.</summary>
        public int SettleTimeoutMs { get; set; } = 15000;

        /// <summary>Serial-poll interval while waiting for phase lock, ms.</summary>
        public int PollIntervalMs { get; set; } = CompletionWaiter.DefaultPollIntervalMs;

        /// <summary>Optional per-poll trace sink (forwarded to the <see cref="CompletionWaiter"/>).</summary>
        public Action<string> Trace { get; set; }

        public Hp8672A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        public double MinFrequencyMHz => 2000.0;
        public double MaxFrequencyMHz => 18000.0;

        /// <summary>Every HP-IB program string sent, in order.</summary>
        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>Device clear resets the 8672A to 3 GHz, modulation off, RF off, ALC internal (manual).</summary>
        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear (DCL/SDC)
            RfOff();
        }

        /// <summary>The 8672A has no program-string preset; a device clear is the documented reset.</summary>
        public void Preset() => _session.Clear();

        /// <summary>Sets the CW output frequency in MHz. Frequency is programmed in kHz as
        /// <c>P&lt;8 digits&gt;Z</c> (one digit per decade, 10 GHz…1 kHz), then EXECUTE.</summary>
        public void SetFrequencyMHz(double mhz)
        {
            long khz = (long)Math.Round(mhz * 1000.0);
            Send(FrequencyCode + khz.ToString("00000000", CultureInfo.InvariantCulture) + ExecuteCode);
        }

        /// <summary>
        /// Sets the output power in dBm, decomposed into a 10-dB RANGE step (0…-110 dBm, argument chars
        /// '0'–';') plus a 1-dB VERNIER (+3…-10 dB). <b>TBD:</b> the RANGE/VERNIER code letters and the vernier
        /// argument encoding are reconstructed — confirm at the bench (#126).
        /// </summary>
        public void SetPowerDbm(double dbm)
        {
            double clamped = Math.Max(-120, Math.Min(3, dbm));
            // Coarse 10-dB range step, then a 1-dB vernier that trims within +3…-10 dB of it.
            int rangeIndex = (int)Math.Floor(-clamped / 10.0);
            rangeIndex = Math.Max(0, Math.Min(11, rangeIndex));         // 0 dBm … -110 dBm
            char rangeArg = (char)('0' + rangeIndex);                   // '0'..';' per the manual's Table 3-7
            int vernier = (int)Math.Round(clamped + rangeIndex * 10.0); // remainder in +3…-10 dB
            Send(string.Empty + RangeCode + rangeArg + VernierCode + vernier.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Enables the RF output (ALC/output program code). <b>TBD:</b> confirm the output code
        /// letter and its RF-on argument at the bench (#126).</summary>
        public void RfOn() => Send(string.Empty + OutputCode + "1");

        /// <summary>Disables the RF output. <b>TBD:</b> confirm the output code letter / RF-off argument (#126).</summary>
        public void RfOff() => Send(string.Empty + OutputCode + "0");

        /// <summary>Reads the raw status byte by serial poll (see the bit constants above).</summary>
        public int ReadStatusByte() => _session.SerialPoll().Value;

        /// <summary>True when the synthesizer is currently phase-locked (the not-phase-locked bit is clear).</summary>
        public bool IsPhaseLocked() => (ReadStatusByte() & NotPhaseLockedBit) == 0;

        /// <summary>
        /// Waits for the synthesizer to re-acquire phase lock after a frequency change — the not-phase-locked
        /// status bit clearing — via the shared <see cref="CompletionWaiter"/> (#96 <c>ExpectBitCleared</c>
        /// direct-bit flow, no enable mask). Throws on timeout.
        /// </summary>
        public void WaitForPhaseLock()
        {
            var channel = new SessionStatusChannel(_session);
            var sw = Stopwatch.StartNew();
            var result = CompletionWaiter.Wait(
                StatusModel(), "HP8672A", "phaseLock", SettleTimeoutMs,
                channel, () => sw.ElapsedMilliseconds, Thread.Sleep, PollIntervalMs, Trace);

            if (result.Outcome != CompletionOutcome.Completed)
            {
                _session.Clear();
                throw new Hp8672AException("synthesizer did not re-acquire phase lock — " + result.Message +
                    ". Check the programmed frequency is in range and the HP-IB extender latency.");
            }
        }

        /// <summary>Convenience: set the frequency and block until the synthesizer re-locks.</summary>
        public void SetFrequencyAndSettleMHz(double mhz)
        {
            SetFrequencyMHz(mhz);
            WaitForPhaseLock();
        }

        /// <summary>
        /// The 8672A's phase-lock settle model: a cleared-settle operation on the not-phase-locked bit (0x08),
        /// direct-bit flow with NO enable mask and NO request-service bit (the fault SRQ is always on and not
        /// per-operation armable — the manual's own worked example polls this bit clearing to detect settle).
        /// Status is read by hardware serial poll. Kept here so it can move to the #41 instrument DB unchanged.
        /// </summary>
        internal static StatusModel StatusModel() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            Bits = new Dictionary<string, int>
            {
                ["overrangePlus10"] = OverrangePlus10Bit,
                ["fmOvermod"] = FmOvermodBit,
                ["levelUncal"] = LevelUncalBit,
                ["notPhaseLocked"] = NotPhaseLockedBit,
                ["rfOff"] = RfOffBit,
                ["freqOutOfRange"] = FreqOutOfRangeBit,
                ["requestService"] = RequestServiceBit,
                ["ovenCold"] = OvenColdBit
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["phaseLock"] = new StatusOperation { ExpectBit = "notPhaseLocked", ExpectBitCleared = true }
            }
        };
    }
}
