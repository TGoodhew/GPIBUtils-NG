using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>Frequency resolution of the HP 5342A, mapped to its <c>SR{n}</c> code.</summary>
    public enum Hp5342AResolution
    {
        /// <summary>1 Hz — <c>SR3</c>.</summary>
        Hz1,
        /// <summary>10 Hz — <c>SR4</c>.</summary>
        Hz10,
        /// <summary>100 Hz — <c>SR5</c>.</summary>
        Hz100,
        /// <summary>1 kHz — <c>SR6</c>.</summary>
        kHz1,
        /// <summary>10 kHz — <c>SR7</c>.</summary>
        kHz10,
        /// <summary>100 kHz — <c>SR8</c>.</summary>
        kHz100,
        /// <summary>1 MHz — <c>SR9</c>.</summary>
        MHz1
    }

    /// <summary>
    /// Driver for the HP 5342A Microwave Frequency Counter (to 18 GHz) — a mnemonic HP-IB instrument
    /// (requires Option 011). Selects AUTO or MANUAL acquisition, sets the manual center frequency and
    /// resolution, and reads the measured frequency (Hz). <b>Reconstructed from the 5342A manual</b> — the
    /// legacy source <c>.cs</c> was a mis-labelled DMM stub with no real 5342A commands, so the mnemonics
    /// here (<c>AU</c>/<c>MA</c>/<c>RE</c>/<c>SR{n}</c>/<c>SM…E</c>) come from the manual and must be
    /// confirmed at the bench. Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para>On the real 5342A a completed measurement asserts SRQ (status-byte bit 7); over/under-level and
    /// out-of-limit conditions talk dashes or all-9s instead of a number — surfaced here as an
    /// <see cref="InvalidOperationException"/>.</para>
    /// </summary>
    public sealed class Hp5342A
    {
        /// <summary>GPIB address of the 5342A. There is no stated numeric factory default (a rear-panel
        /// 5-bit address switch; 31 = talk-only); the manual's programming examples use address 2. Override
        /// with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::2::INSTR";

        /// <summary>Manual center frequency must be below this (Hz) per the manual (&lt; 18 GHz).</summary>
        public const double MaxCenterFrequencyHz = 18e9;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp5342A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        public IReadOnlyList<string> History => _history;

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>The 5342A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 5342A Microwave Frequency Counter (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("RE");         // RESET — blanks display, restarts measurement
            Send("AU");         // AUTO acquisition (default operating mode)
        }

        public void Reset() => Send("RE");

        /// <summary>Selects AUTOMATIC acquisition (<c>AU</c>).</summary>
        public void SetAutoMode() => Send("AU");

        /// <summary>Selects MANUAL acquisition (<c>MA</c>) — pair with <see cref="SetManualCenterFrequencyMHz"/>.</summary>
        public void SetManualMode() => Send("MA");

        /// <summary>Sets the manual center frequency in MHz (<c>SM{mhz}E</c>); must be below 18 GHz.</summary>
        public void SetManualCenterFrequencyMHz(double mhz)
        {
            if (mhz <= 0 || mhz * 1e6 >= MaxCenterFrequencyHz)
                throw new ArgumentOutOfRangeException(nameof(mhz), mhz, "Center frequency must be > 0 and < 18000 MHz.");
            // The manual's SM entry is an integer number of MHz; a decimal point makes the string ignored.
            Send("SM" + ((long)Math.Round(mhz)).ToString(CultureInfo.InvariantCulture) + "E");
        }

        internal static string ResolutionCode(Hp5342AResolution resolution)
        {
            switch (resolution)
            {
                case Hp5342AResolution.Hz1: return "SR3";
                case Hp5342AResolution.Hz10: return "SR4";
                case Hp5342AResolution.Hz100: return "SR5";
                case Hp5342AResolution.kHz1: return "SR6";
                case Hp5342AResolution.kHz10: return "SR7";
                case Hp5342AResolution.kHz100: return "SR8";
                case Hp5342AResolution.MHz1: return "SR9";
                default: throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }

        /// <summary>Sets the frequency resolution (<c>SR{n}</c>).</summary>
        public void SetResolution(Hp5342AResolution resolution) => Send(ResolutionCode(resolution));

        /// <summary>Reads the measured frequency in Hz (the counter talks its reading; a dashes/all-9s
        /// output is an over/under-level or out-of-limit condition and throws).</summary>
        public double ReadFrequency() => ParseFrequency(_session.ReadString());

        /// <summary>Parses a 5342A frequency reading; throws on the dashes / all-9s error indications.</summary>
        internal static double ParseFrequency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 5342A frequency reading.");
            var s = raw.Trim();

            // Over/under-level and out-of-limit conditions are shown as dashes.
            if (s.IndexOf('-') >= 0 && s.IndexOfAny("0123456789".ToCharArray()) < 0)
                throw new InvalidOperationException("5342A signalled an over/under-level condition (dashes) — no valid frequency.");

            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
            {
                var m = System.Text.RegularExpressions.Regex.Match(s, @"[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
                if (!m.Success || !double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out hz))
                    throw new FormatException($"Unrecognized 5342A frequency reading: '{raw}'.");
            }
            return hz;
        }
    }
}
