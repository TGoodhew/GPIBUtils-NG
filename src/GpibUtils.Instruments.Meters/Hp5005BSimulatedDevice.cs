using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// An in-memory model of an HP 5005B for use with <see cref="SimulatedGpibProvider"/>, rich enough to
    /// drive the <see cref="Hp5005B"/> driver — including its vendor <c>QM</c>-mask / data-ready SRQ
    /// measurement handshake (read by hardware serial poll) — with no hardware.
    ///
    /// <para>The instrument free-runs: arming the SRQ mask (<c>QMn</c>, n&gt;0) or selecting a function
    /// (<c>Fn</c>) starts a fresh measurement that stays busy for <see cref="DataReadyPolls"/> serial polls and
    /// then asserts data-ready (0x01) — plus request-service (0x40) when the mask enables it. Set
    /// <see cref="MeasurementCompletes"/> false to model a measurement that never finishes (the waiter times
    /// out) and <see cref="ErrorOnMeasure"/> to raise the error bit (0x04) at completion. The completed reading
    /// is collected by addressing the instrument to talk (any read that is not one of the ID/SE/SU/TH
    /// queries returns the measurement).</para>
    /// </summary>
    public sealed class Hp5005BSimulatedDevice
    {
        private const byte DataReady = 0x01, ProbeSwitch = 0x02, Error = 0x04, PowerOk = 0x20;

        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private int _function;
        private int _qmMask;
        private bool _dataReady;
        private int _busyPolls;
        private bool _error;
        private bool _probeSwitchPushed;

        /// <summary>Every command the instrument was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        public int Function => _function;
        public int ServiceRequestMask => _qmMask;

        /// <summary>Value returned for a numeric-function read (freq/Ω/voltage/…).</summary>
        public double MeasurementValue { get; set; } = 1.2345;

        /// <summary>4-hex signature returned for the signature functions (F0/F1).</summary>
        public string Signature { get; set; } = "A3F0";

        /// <summary>Error code returned by <c>SE</c>.</summary>
        public int ErrorCode { get; set; }

        /// <summary>How many serial polls a measurement stays busy before data-ready asserts (default 2).</summary>
        public int DataReadyPolls { get; set; } = 2;

        /// <summary>When false, a measurement never completes (models a stuck measurement → waiter times out).</summary>
        public bool MeasurementCompletes { get; set; } = true;

        /// <summary>When true, the measurement sets the error bit (0x04) at completion.</summary>
        public bool ErrorOnMeasure { get; set; }

        public Hp5005BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP5005B",
                WriteObserver = Apply,
                Responder = Respond,
                OnSerialPoll = AdvanceMeasurement
            };
            Recompute();
        }

        private void Apply(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper.Length == 0) return;
            _commands.Add(upper);

            if (upper == "ID" || upper == "SE" || upper == "SU" || upper.StartsWith("TH")) return;   // queries
            if (upper == "RS") { _function = 0; _qmMask = 0; _probeSwitchPushed = false; RestartMeasurement(); return; }

            var fm = Regex.Match(upper, @"^F(\d)$");
            if (fm.Success) { _function = fm.Groups[1].Value[0] - '0'; RestartMeasurement(); return; }

            var qm = Regex.Match(upper, @"^QM(\d)$");
            if (qm.Success)
            {
                _qmMask = qm.Groups[1].Value[0] - '0';
                if (_qmMask > 0) RestartMeasurement(); else Recompute();
                return;
            }
            // PCn/PTn/PPn/PQn/TDn/TCn/TQn/PSn/ALn — config; no measurement/status effect worth modelling here.
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
            int conditions = (_dataReady ? DataReady : 0) | (_probeSwitchPushed ? ProbeSwitch : 0) | (_error ? Error : 0);
            Instrument.StatusByte = (byte)(conditions | PowerOk);
            Instrument.ServiceRequestPending = (_qmMask & conditions) != 0;
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (upper == "ID") return "HP5005B";
            if (upper == "SE") return ErrorCode.ToString(CultureInfo.InvariantCulture);
            if (upper == "SU") return "0000";   // raw setup nibbles (placeholder)
            if (upper == "TH1" || upper == "TH2" || upper == "TH3") return "TTL";
            // Any other read = address-to-talk: return the latest completed measurement.
            return _function <= 1
                ? Signature
                : MeasurementValue.ToString("G6", CultureInfo.InvariantCulture);
        }
    }
}
