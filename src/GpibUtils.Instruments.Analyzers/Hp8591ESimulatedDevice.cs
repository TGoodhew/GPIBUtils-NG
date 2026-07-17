using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// An in-memory model of an HP 8591E for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp8591E"/> driver — including its legacy pre-488.2 <b>RQS-mask / <c>STB?</c></b>
    /// sweep handshake — end to end with no hardware. Unlike the 8560E (which is serial-polled), the 8591E
    /// reads its status byte with the <c>STB?</c> query, so the whole completion cycle runs through the
    /// <see cref="SimulatedInstrument.Responder"/>: each <c>STB?</c> both reports the status byte and advances
    /// the busy→done sweep one step (mirroring how a real controller polls it).
    ///
    /// <para>Status-byte model (8590 family): end-of-sweep (0x04) is a condition that is TRUE while idle,
    /// goes false when a sweep starts (<c>TS</c>), and true again when it finishes; request-service (0x40)
    /// is asserted whenever an <c>RQS</c>-masked condition is currently true. A triggered sweep stays busy for
    /// <see cref="BusyPolls"/> <c>STB?</c> reads and then completes; set <see cref="SweepCompletes"/> false to
    /// model a stuck sweep (the waiter times out) and <see cref="ErrorOnSweep"/> to raise the illegal-command
    /// bit (0x20) at completion.</para>
    /// </summary>
    public sealed class Hp8591ESimulatedDevice
    {
        private const int EndOfSweep = 0x04, IllegalCommand = 0x20, RequestService = 0x40;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        private enum Sweep { Idle, Busy, Done }
        private Sweep _sweep = Sweep.Idle;
        private int _busyPollsRemaining;
        private int _mask;
        private bool _endOfSweep = true;
        private bool _illegalCommand;

        /// <summary>Every command the analyzer was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public double CenterFrequencyHz { get; private set; }
        public double SpanHz { get; private set; }
        public double ResolutionBandwidthHz { get; private set; }
        public double VideoBandwidthHz { get; private set; }
        public double SweepTimeSeconds { get; private set; }
        public bool SingleSweepMode { get; private set; }

        /// <summary>Trace amplitudes returned by <c>TRA?</c>.</summary>
        public double[] Trace { get; set; } = { -70, -55, -30, -55, -70 };

        /// <summary>Marker frequency (Hz) returned by <c>MKF?</c>.</summary>
        public double MarkerFrequencyHz { get; set; } = 3e8;

        /// <summary>Marker amplitude (dBm) returned by <c>MKA?</c>. <c>MKPK HI</c> sets it to the trace peak.</summary>
        public double MarkerAmplitudeDbm { get; set; } = -30;

        /// <summary>How many <c>STB?</c> reads a triggered sweep stays busy before completing (default 1).</summary>
        public int BusyPolls { get; set; } = 1;

        /// <summary>When false, a triggered sweep never completes (models a stuck sweep → waiter times out).</summary>
        public bool SweepCompletes { get; set; } = true;

        /// <summary>When true, the sweep sets the illegal-command bit (0x20) at completion (models an error).</summary>
        public bool ErrorOnSweep { get; set; }

        public Hp8591ESimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP8591E",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var raw = command.Trim();
            if (raw.Length == 0) return;
            foreach (var part in raw.Split(';'))
            {
                var cmd = part.Trim();
                if (cmd.Length == 0) continue;
                _commands.Add(cmd);
                var upper = cmd.ToUpperInvariant();

                if (upper == "IP")
                {
                    _sweep = Sweep.Idle; _mask = 0; _endOfSweep = true; _illegalCommand = false;
                    CenterFrequencyHz = SpanHz = ResolutionBandwidthHz = VideoBandwidthHz = SweepTimeSeconds = 0;
                    continue;
                }
                if (upper.StartsWith("RQS"))
                {
                    _mask = int.TryParse(upper.Substring(3).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
                    continue;
                }
                if (upper == "CLS") { _endOfSweep = false; _illegalCommand = false; continue; }
                if (upper == "SNGLS") { SingleSweepMode = true; continue; }
                if (upper == "CONTS") { SingleSweepMode = false; continue; }
                if (upper == "TS")
                {
                    _sweep = Sweep.Busy; _busyPollsRemaining = Math.Max(0, BusyPolls);
                    _endOfSweep = false; _illegalCommand = false; continue;
                }
                if (upper.StartsWith("MKPK")) { MarkerAmplitudeDbm = Trace != null && Trace.Length > 0 ? Trace.Max() : MarkerAmplitudeDbm; continue; }
                if (TryValue(upper, "CF", out var cf)) { CenterFrequencyHz = cf; continue; }
                if (TryValue(upper, "SP", out var sp)) { SpanHz = sp; continue; }
                if (TryValue(upper, "RB", out var rb)) { ResolutionBandwidthHz = rb; continue; }
                if (TryValue(upper, "VB", out var vb)) { VideoBandwidthHz = vb; continue; }
                if (TryValue(upper, "ST", out var st)) { SweepTimeSeconds = st; continue; }
            }
        }

        /// <summary>Advances the busy→done sweep one <c>STB?</c> read (the arm-then-poll completion handshake).</summary>
        private void AdvanceSweep()
        {
            if (_sweep != Sweep.Busy) return;
            if (_busyPollsRemaining > 0) { _busyPollsRemaining--; return; }
            if (!SweepCompletes) return;   // stuck sweep — never completes
            _sweep = Sweep.Done;
            _endOfSweep = true; _illegalCommand = ErrorOnSweep;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            _commands.Add(upper);
            if (upper == "ID?") return Instrument.IdentificationString;
            if (upper == "STB?")
            {
                AdvanceSweep();
                int conditions = (_endOfSweep ? EndOfSweep : 0) | (_illegalCommand ? IllegalCommand : 0);
                int rqs = (_mask & conditions) != 0 ? RequestService : 0;
                return (conditions | rqs).ToString(CultureInfo.InvariantCulture);
            }
            if (upper == "CF?") return CenterFrequencyHz.ToString("G9", CultureInfo.InvariantCulture);
            if (upper == "TRA?")
                return string.Join(",", (Trace ?? Array.Empty<double>()).Select(v => v.ToString("G6", CultureInfo.InvariantCulture)));
            if (upper == "MKF?") return MarkerFrequencyHz.ToString("G9", CultureInfo.InvariantCulture) + " HZ";
            if (upper == "MKA?") return MarkerAmplitudeDbm.ToString("G6", CultureInfo.InvariantCulture) + " DBM";
            return null;
        }

        private static bool TryValue(string upper, string mnemonic, out double value)
        {
            value = 0;
            if (!upper.StartsWith(mnemonic + " ") && upper != mnemonic) return false;
            var m = Regex.Match(upper, @"[-+0-9.]+(?:E[-+]?[0-9]+)?");
            return m.Success && double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
