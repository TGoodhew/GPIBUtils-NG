using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Audio
{
    /// <summary>
    /// An in-memory model of an HP 8903B for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp8903B"/> driver — including its Special-Function-22 / data-ready SRQ measurement
    /// handshake (read by hardware serial poll) — with no hardware. The settled trigger (<c>T3</c>) starts a
    /// measurement that stays busy for <see cref="DataReadyPolls"/> serial polls, then asserts data-ready
    /// (0x01) and, when Special Function 22 enables it, a service request (0x40). Set
    /// <see cref="MeasurementCompletes"/> false to model a stuck measurement (the waiter times out) and
    /// <see cref="ErrorOnMeasure"/> to raise the instrument-error bit (0x04) at completion.
    ///
    /// <para>NOTE: the real 8903B re-triggers a measurement on every serial poll; this simulator does not
    /// model that hardware quirk (it is a bench-verification caveat noted on the driver), so the standard
    /// poll-loop completion is exercised cleanly here.</para>
    /// </summary>
    public sealed class Hp8903BSimulatedDevice
    {
        private const byte DataReady = 0x01, HpibError = 0x02, InstrumentError = 0x04;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private int _srqEnableMask = 2;   // power-up default 22.2 (HP-IB-error only)
        private bool _dataReady;
        private int _busyPolls;
        private bool _error;

        /// <summary>Every program string the instrument was sent, in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public int ServiceRequestConditionMask => _srqEnableMask;

        /// <summary>Value returned as the measurement result.</summary>
        public double MeasurementValue { get; set; } = 1.2345;

        /// <summary>How many serial polls a measurement stays busy before data-ready asserts (default 2).</summary>
        public int DataReadyPolls { get; set; } = 2;

        /// <summary>When false, a measurement never completes (models a stuck measurement → waiter times out).</summary>
        public bool MeasurementCompletes { get; set; } = true;

        /// <summary>When true, the measurement sets the instrument-error bit (0x04) at completion.</summary>
        public bool ErrorOnMeasure { get; set; }

        public Hp8903BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP8903B",
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

            // Special Function 22 (Service Request Condition): "22.<n>SP".
            var sf = Regex.Match(upper, @"^22\.(\d)SP$");
            if (sf.Success) { _srqEnableMask = sf.Groups[1].Value[0] - '0'; Recompute(); return; }

            if (upper == "T3" || upper == "T2") { RestartMeasurement(); return; }
            // T0/T1 (free run / hold), AU, FR.../AP.../M#/S#/A0/A1 — configuration, no completion effect here.
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
            int conditions = (_dataReady ? DataReady : 0) | (_error ? InstrumentError : 0);
            Instrument.StatusByte = (byte)conditions;
            Instrument.ServiceRequestPending = (_srqEnableMask & conditions) != 0;
        }

        // Any read = the 12-byte output: a signed scientific literal the driver parses.
        private string Respond(string command) =>
            MeasurementValue.ToString("E5", CultureInfo.InvariantCulture);
    }
}
