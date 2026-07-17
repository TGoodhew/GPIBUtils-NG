using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SourceMeasure
{
    /// <summary>
    /// Driver for the Keithley 2400 SourceMeter SMU — a SCPI (IEEE-488.2) source-measure unit. Sources voltage
    /// or current with a compliance limit and reads back V/I/R with a single blocking <c>:READ?</c>.
    /// Reconstructed from the 2400 User's Manual (issue #134); first implementer of
    /// <see cref="ISourceMeasureUnit"/>. Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Keithley2400 : ISourceMeasureUnit
    {
        /// <summary>GPIB address of the 2400 — factory default 24 (Keithley 2400 default). Override with
        /// <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::24::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private SmuSourceFunction _source = SmuSourceFunction.Voltage;

        public Keithley2400(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            Send(":FORMat:ELEMents VOLTage,CURRent,RESistance");   // :READ? returns V,I,R
        }

        public void Reset() => Send("*RST");

        public void SetSourceFunction(SmuSourceFunction function)
        {
            _source = function;
            Send(":SOURce:FUNCtion " + (function == SmuSourceFunction.Voltage ? "VOLTage" : "CURRent"));
        }

        public void SetSourceLevel(double value)
        {
            string node = _source == SmuSourceFunction.Voltage ? "VOLTage" : "CURRent";
            Send(":SOURce:" + node + ":LEVel " + value.ToString("G7", CultureInfo.InvariantCulture));
        }

        /// <summary>Sets the compliance limit — a current limit when sourcing voltage
        /// (<c>:SENSe:CURRent:PROTection</c>), or a voltage limit when sourcing current
        /// (<c>:SENSe:VOLTage:PROTection</c>).</summary>
        public void SetCompliance(double limit)
        {
            string node = _source == SmuSourceFunction.Voltage ? "CURRent" : "VOLTage";
            Send(":SENSe:" + node + ":PROTection " + limit.ToString("G7", CultureInfo.InvariantCulture));
        }

        public void SetOutput(bool on) => Send(":OUTPut " + (on ? "ON" : "OFF"));

        /// <summary>Triggers a source-measure and reads V/I/R (<c>:READ?</c> with the V,I,R element list).</summary>
        public SmuReading Measure() => ParseReading(Query(":READ?"));

        internal static SmuReading ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty 2400 :READ? reply.");
            var fields = raw.Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v : throw new FormatException($"Unrecognized 2400 reading field: '{t}'."))
                .ToArray();
            if (fields.Length < 3)
                throw new FormatException($"Expected V,I,R in 2400 :READ? reply: '{raw}'.");
            return new SmuReading(fields[0], fields[1], fields[2]);
        }
    }
}
