using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>
    /// Driver for the Rigol DM3058 5½-digit digital multimeter — a SCPI instrument driven with one-shot
    /// <c>MEASure:…?</c> queries (auto-range, auto-trigger). Ported from <c>GPIBUtils</c> (issue #26, the
    /// current version; supersedes the older #30, which mis-mapped AC current to the DC command). Reuses
    /// <see cref="MeasurementFunction"/> for the function set. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>The legacy app drove the DM3058 over LXI/TCP-IP; this driver is transport-neutral and defaults
    /// to the instrument's GPIB address.</para>
    /// </summary>
    public sealed class RigolDm3058 : IDigitalMultimeter
    {
        /// <summary>GPIB address of the DM3058 — its documented factory-default GPIB address is 7 (DM3058
        /// User's Guide: "The default address is 7 when the instrument is shipped from the factory").
        /// Override with <c>--address</c>. The legacy app used LXI (<c>TCPIP0::…::inst0::INSTR</c>).</summary>
        public const string DefaultResource = "GPIB0::7::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public RigolDm3058(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

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

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
        }

        public void Reset() => Send("*RST");

        /// <summary>Maps a function to the DM3058 one-shot measurement query.</summary>
        internal static string MeasureQuery(MeasurementFunction function)
        {
            switch (function)
            {
                case MeasurementFunction.DcVoltage: return "MEAS:VOLT:DC?";
                case MeasurementFunction.AcVoltage: return "MEAS:VOLT:AC?";
                case MeasurementFunction.DcCurrent: return "MEAS:CURR:DC?";
                case MeasurementFunction.AcCurrent: return "MEAS:CURR:AC?";
                case MeasurementFunction.Resistance2Wire: return "MEAS:RES?";
                case MeasurementFunction.Resistance4Wire: return "MEAS:FRES?";
                case MeasurementFunction.Frequency: return "MEAS:FREQ?";
                case MeasurementFunction.Period: return "MEAS:PER?";
                case MeasurementFunction.Continuity: return "MEAS:CONT?";
                case MeasurementFunction.Diode: return "MEAS:DIOD?";
                default: throw new ArgumentOutOfRangeException(nameof(function), function, null);
            }
        }

        /// <summary>Takes a one-shot measurement of the given function and returns the reading.</summary>
        public double Measure(MeasurementFunction function) => ParseReading(Query(MeasureQuery(function)));

        public double MeasureDcVoltage() => Measure(MeasurementFunction.DcVoltage);
        public double MeasureAcVoltage() => Measure(MeasurementFunction.AcVoltage);
        public double MeasureDcCurrent() => Measure(MeasurementFunction.DcCurrent);
        public double MeasureAcCurrent() => Measure(MeasurementFunction.AcCurrent);
        public double MeasureResistance() => Measure(MeasurementFunction.Resistance2Wire);

        // ---- IDigitalMultimeter (one-shot MEAS? semantics) ----------------------

        private MeasurementFunction _configured = MeasurementFunction.DcVoltage;

        /// <summary>Selects the function used by <see cref="ReadValue"/> (the DM3058 measures one-shot per read).</summary>
        public void Configure(MeasurementFunction function, string range = null, string resolution = null) =>
            _configured = function;

        public string QueryConfiguration() => Query("FUNC?");
        public string QueryFunction() => Query("FUNC?");

        public double ReadValue() => Measure(_configured);
        public double[] ReadValues(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be >= 1.");
            var values = new double[count];
            for (int i = 0; i < count; i++) values[i] = Measure(_configured);
            return values;
        }
        public double[] Fetch() => new[] { Measure(_configured) };
        public void Initiate() { /* one-shot instrument: no separate arm */ }
        public void BusTrigger() => Send("*TRG");
        public void SetTriggerSource(TriggerSource source) { /* DM3058 auto-triggers on MEAS? */ }
        public void SetTriggerCount(int count) { }
        public void SetSampleCount(int count) { }
        public void SetNplc(MeasurementFunction function, double nplc) =>
            Send($"{Root(function)}:NPLC {nplc.ToString("G7", CultureInfo.InvariantCulture)}");
        public void SetAutoRange(MeasurementFunction function, bool on) =>
            Send($"{Root(function)}:RANG:AUTO {(on ? "ON" : "OFF")}");
        public string NextError() => Query("SYST:ERR?");
        public IReadOnlyList<string> DrainErrors()
        {
            var errors = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                var e = NextError();
                if (string.IsNullOrEmpty(e) || e.TrimStart('+', ' ').StartsWith("0", StringComparison.Ordinal)) break;
                errors.Add(e);
            }
            return errors;
        }
        public bool SelfTest()
        {
            var r = Query("*TST?");
            return r == "+0" || r == "0";
        }

        private static string Root(MeasurementFunction function)
        {
            switch (function)
            {
                case MeasurementFunction.DcVoltage: return ":VOLT:DC";
                case MeasurementFunction.AcVoltage: return ":VOLT:AC";
                case MeasurementFunction.DcCurrent: return ":CURR:DC";
                case MeasurementFunction.AcCurrent: return ":CURR:AC";
                case MeasurementFunction.Resistance2Wire: return ":RES";
                case MeasurementFunction.Resistance4Wire: return ":FRES";
                default: throw new ArgumentException($"{function} is not a rangeable DM3058 function.", nameof(function));
            }
        }

        /// <summary>Parses a DM3058 reading. The SCPI ±9.9E37 over-range / NaN sentinel is rejected
        /// (<see cref="InvalidOperationException"/>), not returned.</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty DM3058 reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized DM3058 reading: '{raw}'.");
            return ScpiReading.Guard(v, raw.Trim(), "DM3058");
        }
    }
}
