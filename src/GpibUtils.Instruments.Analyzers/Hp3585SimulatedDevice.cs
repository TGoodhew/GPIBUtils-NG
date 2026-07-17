using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// An in-memory model of an HP 3585B for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp3585"/> driver — including its legacy <c>CQ</c>-enabled operation-complete SRQ
    /// handshake (read by hardware serial poll) — end to end with no hardware.
    ///
    /// <para>Status-byte model (3585B): operation-complete (0x08) is set at end-of-sweep when <c>CQ</c> is
    /// enabled and cleared by <c>S1/S2/S3</c>/<c>CC</c>; request-service (0x40) is OR-ed in by the simulated
    /// session when a service request is pending. A single sweep is armed by <c>S2;T5;</c> and stays busy for
    /// <see cref="BusyPolls"/> serial polls before completing; set <see cref="SweepCompletes"/> false to model
    /// a stuck sweep and <see cref="ErrorOnSweep"/> to raise the syntax-error bit (0x01) at completion. Marker
    /// and trace reads are served through the <c>D1</c>/<c>D2</c>/<c>D3</c> dump queries.</para>
    /// </summary>
    public sealed class Hp3585SimulatedDevice
    {
        private const byte SyntaxError = 0x01, OperationComplete = 0x08;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        private enum Sweep { Idle, Busy, Done }
        private Sweep _sweep = Sweep.Idle;
        private int _busyPollsRemaining;
        private bool _cqEnabled;
        private bool _operationComplete;
        private bool _syntaxError;

        /// <summary>Every command the analyzer was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public double CenterFrequencyHz { get; private set; }
        public double SpanHz { get; private set; }
        public double ResolutionBandwidthHz { get; private set; }
        public double VideoBandwidthHz { get; private set; }
        public double ReferenceLevelDb { get; private set; }
        public bool SingleSweepMode { get; private set; }
        public bool OperationCompleteSrqEnabled => _cqEnabled;

        /// <summary>Trace amplitudes returned by the <c>D3</c> dump.</summary>
        public double[] Trace { get; set; } = { -90, -70, -25, -70, -90 };

        /// <summary>Marker frequency (Hz) returned by the <c>D2</c> dump.</summary>
        public double MarkerFrequencyHz { get; set; } = 1e7;

        /// <summary>Marker amplitude (dB) returned by the <c>D1</c>/<c>D2</c> dumps.</summary>
        public double MarkerAmplitudeDb { get; set; } = -25;

        /// <summary>How many serial polls a triggered sweep stays busy before completing (default 1).</summary>
        public int BusyPolls { get; set; } = 1;

        /// <summary>When false, a triggered sweep never completes (models a stuck sweep → waiter times out).</summary>
        public bool SweepCompletes { get; set; } = true;

        /// <summary>When true, the sweep sets the syntax-error bit (0x01) at completion (models an error).</summary>
        public bool ErrorOnSweep { get; set; }

        public Hp3585SimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP3585B",
                WriteObserver = Apply,
                Responder = Respond,
                OnSerialPoll = AdvanceSweep
            };
            Recompute();
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

                if (upper == "PR")
                {
                    _sweep = Sweep.Idle; _cqEnabled = false; _operationComplete = false; _syntaxError = false;
                    CenterFrequencyHz = SpanHz = ResolutionBandwidthHz = VideoBandwidthHz = ReferenceLevelDb = 0;
                    Recompute(); continue;
                }
                if (upper == "CQ") { _cqEnabled = true; Recompute(); continue; }
                if (upper == "CC") { _cqEnabled = false; Recompute(); continue; }
                if (upper == "S1") { SingleSweepMode = false; _operationComplete = false; Recompute(); continue; }
                if (upper == "S2") { SingleSweepMode = true; _operationComplete = false; Recompute(); continue; }
                if (upper == "S3") { _operationComplete = false; Recompute(); continue; }
                if (upper == "T5")   // delayed trigger — starts the (single) sweep
                {
                    _sweep = Sweep.Busy; _busyPollsRemaining = Math.Max(0, BusyPolls);
                    _operationComplete = false; _syntaxError = false; Recompute(); continue;
                }
                if (TryValue(upper, "CF", out var cf)) { CenterFrequencyHz = cf; continue; }
                if (TryValue(upper, "FS", out var fs)) { SpanHz = fs; continue; }
                if (TryValue(upper, "RB", out var rb)) { ResolutionBandwidthHz = rb; continue; }
                if (TryValue(upper, "VB", out var vb)) { VideoBandwidthHz = vb; continue; }
                if (TryValue(upper, "RL", out var rl)) { ReferenceLevelDb = rl; continue; }
            }
        }

        /// <summary>Advances the busy→done sweep one serial poll (the SRQ-edge handshake).</summary>
        private void AdvanceSweep()
        {
            if (_sweep != Sweep.Busy) return;
            if (_busyPollsRemaining > 0) { _busyPollsRemaining--; Recompute(); return; }
            if (!SweepCompletes) return;   // stuck sweep — never completes
            _sweep = Sweep.Done;
            _operationComplete = true; _syntaxError = ErrorOnSweep;
            Recompute();
        }

        /// <summary>Publishes condition bits into the status byte and flags a pending service request when
        /// operation-complete is enabled (<c>CQ</c>) and set, or a syntax error occurred.</summary>
        private void Recompute()
        {
            Instrument.StatusByte = (byte)((_operationComplete ? OperationComplete : 0) | (_syntaxError ? SyntaxError : 0));
            Instrument.ServiceRequestPending = (_cqEnabled && _operationComplete) || _syntaxError;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "D3")
                return string.Join(",", (Trace ?? Array.Empty<double>()).Select(v => v.ToString("G6", CultureInfo.InvariantCulture)));
            if (upper == "D2")
                return MarkerFrequencyHz.ToString("G9", CultureInfo.InvariantCulture) + "," +
                       MarkerAmplitudeDb.ToString("G6", CultureInfo.InvariantCulture);
            if (upper == "D1")
                return MarkerAmplitudeDb.ToString("G6", CultureInfo.InvariantCulture);
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
