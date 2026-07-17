using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// Shared driver for the LeCroy DSO command family (LC-series, WaveRunner) — the <c>Cn:</c>-prefixed
    /// ASCII dialect (IEEE-488.2-framed, not SCPI): <c>TRMD</c> trigger/run mode, <c>Cn:TRA</c> trace on/off,
    /// <c>ASET</c> auto-setup, <c>Cn:PAVA?</c> parameter measurement. Runs over any
    /// <see cref="IInstrumentSession"/> (issues #135/#140).
    ///
    /// <para><b>Bench-confirm.</b> Neither issue had a readable Remote Control Manual, so these mnemonics are
    /// domain knowledge, not manual-confirmed — validate command/reply syntax and the default GPIB address at
    /// the bench before trusting this driver.</para>
    /// </summary>
    public abstract class LeCroyScope : IOscilloscope
    {
        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private readonly int _channelCount;

        protected LeCroyScope(IInstrumentSession session, int channelCount)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _channelCount = channelCount;
        }

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        private int Check(int ch)
        {
            if (ch < 1 || ch > _channelCount)
                throw new ArgumentOutOfRangeException(nameof(ch), ch, $"Channel must be 1-{_channelCount}.");
            return ch;
        }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("*CLS"); }

        public void Run() => Send("TRMD AUTO");
        public void Stop() => Send("TRMD STOP");
        public void Single() => Send("TRMD SINGLE");
        public void AutoScale() => Send("ASET");

        public void SetChannelDisplay(int channel, bool on) =>
            Send("C" + Check(channel) + ":TRA " + (on ? "ON" : "OFF"));

        /// <summary>Peak-to-peak volts — shorthand for <see cref="Measure"/>.</summary>
        public double MeasureVpp(int channel) => Measure(channel, ScopeMeasurementType.PeakToPeak);

        /// <summary>Takes an automatic measurement (<c>Cn:PAVA? &lt;param&gt;</c>; reply e.g.
        /// <c>C1:PAVA PKPK,4.96E-01,V</c>).</summary>
        public double Measure(int channel, ScopeMeasurementType type) =>
            ParseReading(Query("C" + Check(channel) + ":PAVA? " + Parameter(type)));

        private static string Parameter(ScopeMeasurementType type)
        {
            switch (type)
            {
                case ScopeMeasurementType.PeakToPeak: return "PKPK";
                case ScopeMeasurementType.Maximum: return "MAX";
                case ScopeMeasurementType.Minimum: return "MIN";
                case ScopeMeasurementType.Amplitude: return "AMPL";
                case ScopeMeasurementType.Mean: return "MEAN";
                case ScopeMeasurementType.Rms: return "RMS";
                case ScopeMeasurementType.Frequency: return "FREQ";
                case ScopeMeasurementType.Period: return "PER";
                case ScopeMeasurementType.RiseTime: return "RISE";
                case ScopeMeasurementType.FallTime: return "FALL";
                default: throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Parses a LeCroy reading — a bare number, or a <c>Cn:PAVA PKPK,&lt;value&gt;,&lt;unit&gt;</c>
        /// reply (returns the first comma-separated field that parses as a number).</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty LeCroy scope reading.");
            foreach (var token in raw.Split(','))
                if (double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return v;
            throw new FormatException($"Unrecognized LeCroy scope reading: '{raw}'.");
        }
    }

    /// <summary>LeCroy LC574A digital storage oscilloscope (#135). GPIB/RS-232.</summary>
    public sealed class LeCroyLC574A : LeCroyScope
    {
        /// <summary>Default GPIB address — <b>provisional</b> (not in the readable manual); confirm at bench.</summary>
        public const string DefaultResource = "GPIB0::4::INSTR";
        public LeCroyLC574A(IInstrumentSession session) : base(session, 4) { }
    }

    /// <summary>LeCroy WaveRunner 6000-series digital storage oscilloscope (#140). LAN/VICP or optional GPIB.</summary>
    public sealed class LeCroyWaveRunner6000 : LeCroyScope
    {
        /// <summary>Default GPIB address — <b>provisional</b>; the native transport is LAN/VICP. Confirm at bench.</summary>
        public const string DefaultResource = "GPIB0::4::INSTR";
        public LeCroyWaveRunner6000(IInstrumentSession session) : base(session, 4) { }
    }
}
