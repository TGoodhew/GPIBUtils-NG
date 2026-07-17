using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.LcrMeters
{
    /// <summary>
    /// An in-memory model of an HP 4275A for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp4275A"/> driver — including its <c>I1</c>-armed Data-Ready SRQ measurement
    /// handshake (read by hardware serial poll) — with no hardware. Execute (<c>E</c>) starts a measurement
    /// that stays busy for <see cref="DataReadyPolls"/> serial polls, then asserts data-ready (bit 1) and, if
    /// Data-Ready SRQ is enabled (<c>I1</c>), a service request (0x40). Set <see cref="MeasurementCompletes"/>
    /// false to model a stuck measurement (the waiter times out) and <see cref="ErrorOnMeasure"/> to raise the
    /// error bit (0x08) at completion. The reading is returned as a simplified Format-A string on any read.
    /// </summary>
    public sealed class Hp4275ASimulatedDevice
    {
        private const byte DataReady = 0x01, Error = 0x08;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private bool _dataReadyEnabled;
        private bool _dataReady;
        private int _busyPolls;
        private bool _error;

        /// <summary>Every program string the instrument was sent, in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public int PrimaryFunction { get; private set; }
        public int TestFrequencyCode { get; private set; }
        public int CircuitMode { get; private set; }
        public bool DataReadySrqEnabled => _dataReadyEnabled;

        /// <summary>Display A (primary) reading returned in Format A.</summary>
        public double Primary { get; set; } = 1.234e-9;

        /// <summary>Display B (secondary) reading returned in Format A.</summary>
        public double Secondary { get; set; } = 5.0e-3;

        /// <summary>How many serial polls a measurement stays busy before data-ready asserts (default 2).</summary>
        public int DataReadyPolls { get; set; } = 2;

        /// <summary>When false, a measurement never completes (models a stuck measurement → waiter times out).</summary>
        public bool MeasurementCompletes { get; set; } = true;

        /// <summary>When true, the measurement sets the error bit (0x08) at completion.</summary>
        public bool ErrorOnMeasure { get; set; }

        public Hp4275ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP4275A",
                WriteObserver = Apply,
                Responder = Respond,
                OnSerialPoll = AdvanceMeasurement
            };
            Recompute();
        }

        private void Apply(string command)
        {
            var raw = (command ?? string.Empty).Trim();
            if (raw.Length == 0) return;
            var upper = raw.ToUpperInvariant();
            _commands.Add(upper);

            if (upper == "I1") { _dataReadyEnabled = true; Recompute(); return; }
            if (upper == "I0") { _dataReadyEnabled = false; Recompute(); return; }
            if (upper == "E") { RestartMeasurement(); return; }

            var am = Regex.Match(upper, @"^A(\d)$"); if (am.Success) { PrimaryFunction = am.Groups[1].Value[0] - '0'; return; }
            var cm = Regex.Match(upper, @"^C(\d)$"); if (cm.Success) { CircuitMode = cm.Groups[1].Value[0] - '0'; return; }
            var fm = Regex.Match(upper, @"^F(\d+)$"); if (fm.Success) { TestFrequencyCode = int.Parse(fm.Groups[1].Value); return; }
            // T1/T2/T3, Z0/ZS, ranges etc. — no status effect worth modelling here.
        }

        private void RestartMeasurement()
        {
            _dataReady = false; _busyPolls = Math.Max(0, DataReadyPolls); _error = false;
            Recompute();
        }

        /// <summary>Advances the busy→data-ready measurement one serial poll.</summary>
        private void AdvanceMeasurement()
        {
            if (_dataReady) return;
            if (_busyPolls > 0) { _busyPolls--; return; }
            if (!MeasurementCompletes) return;
            _dataReady = true; _error = ErrorOnMeasure;
            Recompute();
        }

        private void Recompute()
        {
            Instrument.StatusByte = (byte)((_dataReady ? DataReady : 0) | (_error ? Error : 0));
            Instrument.ServiceRequestPending = (_dataReadyEnabled && _dataReady) || _error;
        }

        // Any read = the Format-A output: "A<primary>,B<secondary>" (simplified; prefixes keep the leading
        // numeric tokens unambiguous for the driver's parser).
        private string Respond(string command) =>
            "A" + Primary.ToString("E4", CultureInfo.InvariantCulture) +
            ",B" + Secondary.ToString("E4", CultureInfo.InvariantCulture);
    }
}
