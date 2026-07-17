using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.ModulationDomain
{
    /// <summary>
    /// Driver for the HP 53310A Modulation Domain Analyzer — a SCPI instrument that measures frequency-vs-time
    /// or time-interval-vs-time (with optional histograms). Uses the <c>:CONFigure</c> + <c>:READ?</c> flow;
    /// <c>:READ?</c> initiates and blocks for the configured measurement. Reconstructed from the 53310A
    /// Programming Reference Manual (issue #113); first implementer of <see cref="IModulationDomainAnalyzer"/>.
    /// Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>Completion uses the blocking <c>:READ?</c> here; the manual also documents an SRQ-edge path
    /// (<c>:STATus:OPERation</c> bit 4 negative-transition → <c>*SRE</c> → SRQ) that maps onto the shared
    /// <c>GpibUtils.Visa.Srq</c> engine — a follow-up if async completion is needed.</para>
    /// </summary>
    public sealed class Hp53310A : IModulationDomainAnalyzer
    {
        /// <summary>GPIB address of the 53310A — the Programming Reference examples use address 12. Override
        /// with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::12::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp53310A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        /// <summary>Backstop for the blocking <c>:READ?</c>, ms (a captured record can take a while).</summary>
        public int ReadTimeoutMs { get; set; } = 30000;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command, int timeoutMs)
        {
            _history.Add(command);
            int prior = _session.TimeoutMilliseconds;
            try { _session.TimeoutMilliseconds = timeoutMs; return (_session.Query(command) ?? string.Empty).Trim(); }
            finally { _session.TimeoutMilliseconds = prior; }
        }

        public string Identify() => Query("*IDN?", _session.TimeoutMilliseconds);

        public void Initialize()
        {
            _session.Clear();
            Send("*RST");
            Send("*CLS");
            Send(":FORMat:DATA ASCii");
        }

        public void Configure(ModulationMeasurement measurement, int channel = 1)
        {
            if (channel < 1 || channel > 3)
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel must be 1-3.");
            string cmd;
            switch (measurement)
            {
                case ModulationMeasurement.FrequencyVsTime: cmd = ":CONFigure:XTIMe:FREQuency" + channel; break;
                case ModulationMeasurement.TimeIntervalVsTime: cmd = ":CONFigure:XTIMe:TINTerval"; break;
                case ModulationMeasurement.FrequencyHistogram: cmd = ":CONFigure:HISTogram:FREQuency" + channel; break;
                case ModulationMeasurement.TimeIntervalHistogram: cmd = ":CONFigure:HISTogram:TINTerval"; break;
                default: throw new ArgumentOutOfRangeException(nameof(measurement), measurement, null);
            }
            Send(cmd);
        }

        public IReadOnlyList<double> Read() => ParseArray(Query(":READ?", ReadTimeoutMs), ":READ?");

        internal static IReadOnlyList<double> ParseArray(string raw, string what)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException($"Empty 53310A {what} reply.");
            var values = raw.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v : throw new FormatException($"Unrecognized 53310A {what} point: '{t}'."))
                .ToList();
            if (values.Count == 0) throw new FormatException($"No parseable points in 53310A {what}: '{raw}'.");
            return values;
        }
    }
}
