using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 8672A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp8672A"/> driver — including its post-retune phase-lock settle (the #96
    /// <c>ExpectBitCleared</c> path) — with no hardware. A frequency program (<c>P…Z</c>) asserts the
    /// not-phase-locked status bit (0x08) and clears it after <see cref="UnlockPolls"/> serial polls,
    /// modelling the synthesizer re-acquiring lock; set <see cref="RelockCompletes"/> false to model a
    /// frequency that never locks (the settle wait times out).
    /// </summary>
    public sealed class Hp8672ASimulatedDevice
    {
        private const byte NotPhaseLocked = 0x08, RfOff = 0x10;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private bool _notPhaseLocked;
        private int _unlockPollsRemaining;
        private bool _rfOn;

        /// <summary>Every program string the generator was sent, in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public double FrequencyMHz { get; private set; } = 3000;   // device-clear default (3 GHz)
        public double PowerDbm { get; private set; }
        public bool RfOutputOn => _rfOn;

        /// <summary>How many serial polls the synthesizer stays unlocked after a frequency change (default 3).</summary>
        public int UnlockPolls { get; set; } = 3;

        /// <summary>When false, a frequency change never re-locks (models a stuck retune → settle times out).</summary>
        public bool RelockCompletes { get; set; } = true;

        public Hp8672ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP8672A",
                WriteObserver = Apply,
                OnSerialPoll = AdvanceSettle
            };
            Recompute();
        }

        private void Apply(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            if (raw.Length == 0) return;
            _commands.Add(raw);
            var upper = raw.ToUpperInvariant();

            // Frequency: P<8 digits kHz>Z
            var fm = Regex.Match(upper, @"^P(\d+)Z$");
            if (fm.Success)
            {
                if (long.TryParse(fm.Groups[1].Value, out var khz)) FrequencyMHz = khz / 1000.0;
                _notPhaseLocked = true; _unlockPollsRemaining = Math.Max(0, UnlockPolls);
                Recompute(); return;
            }
            // Level: K<rangeArg>L<vernier>
            var lm = Regex.Match(upper, @"^K(.)L(-?\d+)$");
            if (lm.Success)
            {
                int rangeIndex = lm.Groups[1].Value[0] - '0';
                if (int.TryParse(lm.Groups[2].Value, out var vernier)) PowerDbm = -10.0 * rangeIndex + vernier;
                return;
            }
            // Output / RF on-off: O<arg>
            var om = Regex.Match(upper, @"^O(\d+)$");
            if (om.Success) { _rfOn = om.Groups[1].Value != "0"; Recompute(); return; }
        }

        /// <summary>Advances the not-phase-locked → locked settle one serial poll.</summary>
        private void AdvanceSettle()
        {
            if (!_notPhaseLocked) return;
            if (_unlockPollsRemaining > 0) { _unlockPollsRemaining--; return; }
            if (!RelockCompletes) return;   // never re-locks
            _notPhaseLocked = false;
            Recompute();
        }

        private void Recompute() =>
            Instrument.StatusByte = (byte)((_notPhaseLocked ? NotPhaseLocked : 0) | (_rfOn ? 0 : RfOff));
    }
}
