using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Shared driver for modern SCPI spectrum analyzers (Rigol DSA800, Agilent N9320A) — center/span,
    /// single-sweep with an <c>*OPC?</c>-blocking completion, trace read and marker-to-peak. Concrete models
    /// differ only in default resource. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Bench-confirm.</b> Neither issue (#102/#136) had a readable Programming Guide, so these SCPI
    /// tokens are the standard analyzer shape from domain knowledge, not manual-confirmed — verify every
    /// command against the real guide or the bench before trusting the driver.</para>
    /// </summary>
    public abstract class ScpiSpectrumAnalyzer : ISpectrumAnalyzer
    {
        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        protected ScpiSpectrumAnalyzer(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        /// <summary>Backstop for the single-sweep <c>*OPC?</c> wait, ms (applied to the session timeout).</summary>
        public int SweepTimeoutMs { get; set; } = 30000;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        public string Identify() => Query("*IDN?");
        public void Initialize() { _session.Clear(); Send("*CLS"); }
        public void Preset() => Send("*RST");

        public void SetCenterFrequencyHz(double hertz) =>
            Send(":FREQuency:CENTer " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " Hz");

        public void SetSpanHz(double hertz) =>
            Send(":FREQuency:SPAN " + hertz.ToString("0.######", CultureInfo.InvariantCulture) + " Hz");

        /// <summary>Single sweep: continuous off, initiate, then block on <c>*OPC?</c> until complete.</summary>
        public void SingleSweep()
        {
            Send(":INITiate:CONTinuous OFF");
            Send(":INITiate:IMMediate");
            int prior = _session.TimeoutMilliseconds;
            try { _session.TimeoutMilliseconds = SweepTimeoutMs; Query("*OPC?"); }
            finally { _session.TimeoutMilliseconds = prior; }
        }

        public IReadOnlyList<double> ReadTrace() => Hp8560E.ParseTrace(Query(":TRACe:DATA? TRACE1"));

        public double MarkerToPeakAmplitude()
        {
            Send(":CALCulate:MARKer1:MAXimum");
            return Hp8560E.ParseScalar(Query(":CALCulate:MARKer1:Y?"), "MARKer:Y?");
        }

        public double MarkerFrequencyHz() => Hp8560E.ParseScalar(Query(":CALCulate:MARKer1:X?"), "MARKer:X?");
    }

    /// <summary>Rigol DSA800-series spectrum analyzer (#102). SCPI over USB/LAN, or GPIB via a USB-GPIB
    /// converter (default GPIB address 18).</summary>
    public sealed class RigolDsa800 : ScpiSpectrumAnalyzer
    {
        /// <summary>Default GPIB address 18 (via a USB-GPIB converter; native transport is USB/LAN). Override
        /// with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::18::INSTR";
        public RigolDsa800(IInstrumentSession session) : base(session) { }
    }

    /// <summary>Agilent N9320A spectrum analyzer (#136). USB-TMC (no GPIB).</summary>
    public sealed class AgilentN9320A : ScpiSpectrumAnalyzer
    {
        /// <summary>Default resource — <b>provisional</b> USB-TMC placeholder (the N9320A has no GPIB port).
        /// Set the real USB resource with <c>--address</c>.</summary>
        public const string DefaultResource = "USB0::0x0957::0xFFEF::INSTR";
        public AgilentN9320A(IInstrumentSession session) : base(session) { }
    }
}
