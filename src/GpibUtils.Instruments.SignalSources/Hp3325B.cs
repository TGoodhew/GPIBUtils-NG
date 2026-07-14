using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>Output waveform of the HP 3325B, mapped to its <c>FU{n}</c> function code.</summary>
    public enum Hp3325BWaveform
    {
        /// <summary>DC only — <c>FU0</c>.</summary>
        Dc,
        /// <summary>Sine — <c>FU1</c>.</summary>
        Sine,
        /// <summary>Square — <c>FU2</c>.</summary>
        Square,
        /// <summary>Triangle — <c>FU3</c>.</summary>
        Triangle,
        /// <summary>Positive ramp — <c>FU4</c>.</summary>
        PositiveRamp
    }

    /// <summary>
    /// Driver for the HP 3325B Synthesizer / Function Generator — a mnemonic HP-IB instrument. Sets the
    /// waveform (<c>FU{n}</c>), frequency (<c>FR &lt;val&gt; &lt;unit&gt;</c>), amplitude (<c>AM &lt;val&gt;
    /// VO</c>) and DC offset (<c>OF &lt;val&gt; VO</c>), with an amplitude-calibration step (<c>AC</c>).
    /// Consolidated from the two <c>GPIBUtils-Old</c> test apps — the 100 Hz harmonic/THD test (#28) and the
    /// DC-offset test (#29). Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp3325B
    {
        /// <summary>GPIB address of the 3325B — its documented default HP-IB address is 17 (3325B Operating
        /// manual: the HP-IB address is reset to 17 after a memory clear). Override with <c>--address</c>.
        /// Note: both legacy apps hardcoded a bench address of 10.</summary>
        public const string DefaultResource = "GPIB0::17::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp3325B(IInstrumentSession session) =>
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
        }

        public void Reset() => Send("*RST");

        internal static string FunctionCode(Hp3325BWaveform waveform)
        {
            switch (waveform)
            {
                case Hp3325BWaveform.Dc: return "FU0";
                case Hp3325BWaveform.Sine: return "FU1";
                case Hp3325BWaveform.Square: return "FU2";
                case Hp3325BWaveform.Triangle: return "FU3";
                case Hp3325BWaveform.PositiveRamp: return "FU4";
                default: throw new ArgumentOutOfRangeException(nameof(waveform), waveform, null);
            }
        }

        /// <summary>Selects the output waveform (<c>FU{n}</c>).</summary>
        public void SetWaveform(Hp3325BWaveform waveform) => Send(FunctionCode(waveform));

        /// <summary>Sets the output frequency in Hz (<c>FR &lt;val&gt; HZ</c>).</summary>
        public void SetFrequencyHz(double hz) =>
            Send("FR " + hz.ToString("0.#########", CultureInfo.InvariantCulture) + " HZ");

        /// <summary>Sets the output frequency in MHz (<c>FR &lt;val&gt; MH</c>).</summary>
        public void SetFrequencyMHz(double mhz) =>
            Send("FR " + mhz.ToString("0.#########", CultureInfo.InvariantCulture) + " MH");

        /// <summary>Sets the output amplitude in volts (<c>AM &lt;val&gt; VO</c>).</summary>
        public void SetAmplitudeVolts(double volts) =>
            Send("AM " + volts.ToString("0.####", CultureInfo.InvariantCulture) + " VO");

        /// <summary>Sets the DC offset in volts (<c>OF &lt;val&gt; VO</c>).</summary>
        public void SetDcOffsetVolts(double volts) =>
            Send("OF " + volts.ToString("0.####", CultureInfo.InvariantCulture) + " VO");

        /// <summary>Performs an amplitude calibration (<c>AC</c>) — recommended after amplitude/offset changes,
        /// as the 3325B amplitude can drift over time.</summary>
        public void AmplitudeCalibration() => Send("AC");
    }
}
