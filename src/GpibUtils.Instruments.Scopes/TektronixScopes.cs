using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Scopes
{
    /// <summary>
    /// Shared driver for the Tektronix TDS/DPO/MSO SCPI scope family — colon-hierarchical mnemonics
    /// (<c>ACQuire</c>/<c>SELect</c>/<c>AUTOSet</c>/<c>MEASUrement:IMMed</c>). Concrete models (DPO3000/
    /// DPO4000/TDS784) differ only in default resource and channel count. Runs over any
    /// <see cref="IInstrumentSession"/> (issues #100/#101/#139).
    /// </summary>
    public abstract class TektronixScope : IOscilloscope
    {
        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();
        private readonly int _channelCount;

        protected TektronixScope(IInstrumentSession session, int channelCount)
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

        public void Run() { Send("ACQuire:STOPAfter RUNSTop"); Send("ACQuire:STATE RUN"); }
        public void Stop() => Send("ACQuire:STATE STOP");
        public void Single() { Send("ACQuire:STOPAfter SEQuence"); Send("ACQuire:STATE RUN"); }
        public void AutoScale() => Send("AUTOSet EXECute");

        public void SetChannelDisplay(int channel, bool on) =>
            Send("SELect:CH" + Check(channel) + (on ? " ON" : " OFF"));

        /// <summary>Peak-to-peak volts via the immediate-measurement path
        /// (<c>MEASUrement:IMMed:TYPe PK2Pk; :SOURce CH&lt;n&gt;; :VALue?</c>).</summary>
        public double MeasureVpp(int channel)
        {
            Send("MEASUrement:IMMed:TYPe PK2Pk");
            Send("MEASUrement:IMMed:SOURce CH" + Check(channel));
            return ParseReading(Query("MEASUrement:IMMed:VALue?"));
        }

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty Tektronix scope reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized Tektronix scope reading: '{raw}'.");
            return v;
        }
    }

    /// <summary>Tektronix DPO3000/MSO3000-series oscilloscope (#100). GPIB via a TEK-USB-488 adapter.</summary>
    public sealed class TektronixDpo3000 : TektronixScope
    {
        /// <summary>Default resource — GPIB via TEK-USB-488 adapter (native transport is USB/LAN); confirm at
        /// bench. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::1::INSTR";
        public TektronixDpo3000(IInstrumentSession session) : base(session, 4) { }
    }

    /// <summary>Tektronix DPO4000/MSO4000-series oscilloscope (#101). GPIB via a TEK-USB-488 adapter.</summary>
    public sealed class TektronixDpo4000 : TektronixScope
    {
        public const string DefaultResource = "GPIB0::1::INSTR";
        public TektronixDpo4000(IInstrumentSession session) : base(session, 4) { }
    }

    /// <summary>Tektronix TDS784C/TDS784D digitizing oscilloscope (#139). Native GPIB.</summary>
    public sealed class TektronixTds784 : TektronixScope
    {
        public const string DefaultResource = "GPIB0::1::INSTR";
        public TektronixTds784(IInstrumentSession session) : base(session, 4) { }
    }
}
