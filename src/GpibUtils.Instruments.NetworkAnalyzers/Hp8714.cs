using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.NetworkAnalyzers
{
    /// <summary>
    /// Driver for the HP/Agilent 8711C/8712C/8713C/8714C RF network-analyzer family — a native SCPI-1996.0
    /// instrument. Sets the frequency sweep and source power, selects a transmission/reflection ratio
    /// measurement, triggers a single sweep blocking on <c>*OPC?</c> (mandatory before any data read, per the
    /// manual), and reads the formatted trace + peak marker. Reconstructed from the family Programmer's Guide
    /// (issue #127). Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp8714 : INetworkAnalyzer
    {
        /// <summary>GPIB address of the analyzer — factory default 16 ("the factory default address is 16").
        /// Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::16::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp8714(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        /// <summary>Backstop for the single-sweep <c>*OPC?</c> wait, ms.</summary>
        public int SweepTimeoutMs { get; set; } = 30000;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("SYSTem:PRESet"); Send("*CLS"); }

        public void SetStartFrequencyHz(double hertz) =>
            Send("SENSe1:FREQuency:STARt " + hertz.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetStopFrequencyHz(double hertz) =>
            Send("SENSe1:FREQuency:STOP " + hertz.ToString("0.######", CultureInfo.InvariantCulture));

        public void SetSourcePowerDbm(double dbm) =>
            Send("SOURce:POWer " + dbm.ToString("0.###", CultureInfo.InvariantCulture));

        public void SetSweepPoints(int points) =>
            Send("SENSe1:SWEep:POINts " + points.ToString(CultureInfo.InvariantCulture));

        /// <summary>Selects the measured ratio. The 8711-family is a single transmission/reflection path;
        /// S21 / input-B = <c>XFR:POW:RAT 2,0</c> (B/R), S11 / input-A = <c>XFR:POW:RAT 1,0</c> (A/R). The
        /// full-2-port parameters (S12/S22) are not available on this family — confirm the exact ratio codes
        /// at the bench.</summary>
        public void SetMeasurement(NetworkParameter parameter)
        {
            string ratio;
            switch (parameter)
            {
                case NetworkParameter.S21:
                case NetworkParameter.InputB: ratio = "XFR:POW:RAT 2,0"; break;
                case NetworkParameter.S11:
                case NetworkParameter.InputA: ratio = "XFR:POW:RAT 1,0"; break;
                case NetworkParameter.InputR: ratio = "XFR:POW 0"; break;
                default: throw new NotSupportedException($"The 8711-family cannot measure {parameter} (single-path analyzer).");
            }
            Send("SENSe1:FUNCtion '" + ratio + "'");
        }

        public void SingleSweep()
        {
            Send("ABORt");
            Send("INITiate1:CONTinuous OFF");
            Send("INITiate1");
            int prior = _session.TimeoutMilliseconds;
            try { _session.TimeoutMilliseconds = SweepTimeoutMs; Query("*OPC?"); }
            finally { _session.TimeoutMilliseconds = prior; }
        }

        public IReadOnlyList<double> ReadFormattedTrace()
        {
            Send("FORMat:DATA ASCii");
            return NetworkAnalyzerParsing.ParseArray(Query("TRACe:DATA? CH1FDATA"), "TRACe:DATA?");
        }

        public double MarkerToPeakY()
        {
            Send("CALCulate1:MARKer1 ON");
            Send("CALCulate1:MARKer1:MAXimum");
            return NetworkAnalyzerParsing.ParseScalar(Query("CALCulate1:MARKer1:Y?"), "MARKer:Y?");
        }

        public double MarkerFrequencyHz() =>
            NetworkAnalyzerParsing.ParseScalar(Query("CALCulate1:MARKer1:X?"), "MARKer:X?");
    }

    /// <summary>Shared parsing for network-analyzer ASCII replies.</summary>
    internal static class NetworkAnalyzerParsing
    {
        public static double ParseScalar(string raw, string what)
        {
            var first = (raw ?? string.Empty).Split(',')[0].Trim();
            if (!double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized network-analyzer {what} reply: '{raw}'.");
            return v;
        }

        public static IReadOnlyList<double> ParseArray(string raw, string what)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException($"Empty network-analyzer {what} reply.");
            var values = raw.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v : throw new FormatException($"Unrecognized {what} point: '{t}'."))
                .ToList();
            if (values.Count == 0) throw new FormatException($"No parseable points in {what}: '{raw}'.");
            return values;
        }
    }
}
