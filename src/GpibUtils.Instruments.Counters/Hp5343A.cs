using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Counters
{
    /// <summary>Frequency resolution of the HP 5343A, mapped to its <c>SR{n}</c> code.</summary>
    public enum Hp5343AResolution
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
    /// Driver for the HP 5343A Microwave Frequency Counter (10 Hz–26.5 GHz, Option 011) — a mnemonic HP-IB
    /// instrument and the higher-range sibling of the already-migrated HP 5342A (issue #114). Selects AUTO or
    /// MANUAL acquisition, low/high range, resolution and the manual center frequency, and reads the measured
    /// frequency (Hz). Following the 5342A/5351A precedent this is a <b>standalone class</b> (it does not
    /// implement <c>IFrequencyCounter</c>, whose numbered-channel + input-impedance shape does not fit a
    /// single-input, range-code microwave counter). Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Reconstructed from the 5343A Operating/Service Manual</b> — some one-letter codes (e.g. the
    /// MANUAL <c>M</c> and RESET <c>R</c>) are OCR-ambiguous on the photocopied scan and must be confirmed at
    /// the bench. A completed measurement asserts SRQ with status byte 0x50 when an output mode
    /// (<c>ST1</c>/<c>ST2</c>) is armed — the completion path maps onto the shared
    /// <c>GpibUtils.Visa.Srq</c> direct-bit flow (as a follow-up, matching the 5342A). Overload/overflow/
    /// insufficient-signal readings are surfaced as an <see cref="InvalidOperationException"/>.</para>
    /// </summary>
    public sealed class Hp5343A : ILegacyFrequencyCounter
    {
        /// <summary>GPIB address of the 5343A. There is no stated numeric factory default (a rear-panel
        /// 5-bit address switch; a separate TALK-ONLY toggle); the manual's programming examples use address
        /// 2. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::2::INSTR";

        /// <summary>Manual center frequency must be below this (Hz) per the manual (&lt; 26.5 GHz).</summary>
        public const double MaxCenterFrequencyHz = 26.5e9;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp5343A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        private void Send(string command) { _session.Write(command); _history.Add(command); }

        /// <summary>The 5343A predates <c>*IDN?</c>; returns a fixed descriptor.</summary>
        public string Identify() => "HP 5343A Microwave Frequency Counter (no *IDN?)";

        public void Initialize()
        {
            _session.Clear();   // HP-IB device clear
            Send("R");          // RESET — blanks display, restarts measurement
            Send("AU");         // AUTO acquisition (default operating mode)
        }

        /// <summary>Resets the counter (<c>R</c>) — blanks display, restarts measurement.</summary>
        public void Reset() => Send("R");

        /// <summary>Selects AUTOMATIC acquisition (<c>AU</c>).</summary>
        public void SetAutoMode() => Send("AU");

        /// <summary>Selects MANUAL acquisition (<c>M</c>) — pair with <see cref="SetManualCenterFrequencyMHz"/>.
        /// The single-letter <c>M</c> is a bench-confirm item (OCR-ambiguous in the scan).</summary>
        public void SetManualMode() => Send("M");

        /// <summary>Selects the low range, 10 Hz–500 MHz (<c>L</c>).</summary>
        public void SetLowRange() => Send("L");

        /// <summary>Selects the high range, 500 MHz–26.5 GHz (<c>H</c>).</summary>
        public void SetHighRange() => Send("H");

        /// <summary>Sets the manual center frequency in MHz (<c>SM{mhz}E</c>); must be below 26.5 GHz.</summary>
        public void SetManualCenterFrequencyMHz(double mhz)
        {
            if (mhz <= 0 || mhz * 1e6 >= MaxCenterFrequencyHz)
                throw new ArgumentOutOfRangeException(nameof(mhz), mhz, "Center frequency must be > 0 and < 26500 MHz.");
            // The manual's SM entry is an integer number of MHz, max 5 chars; a decimal makes the string ignored.
            Send("SM" + ((long)Math.Round(mhz)).ToString(CultureInfo.InvariantCulture) + "E");
        }

        internal static string ResolutionCode(Hp5343AResolution resolution)
        {
            switch (resolution)
            {
                case Hp5343AResolution.Hz1: return "SR3";
                case Hp5343AResolution.Hz10: return "SR4";
                case Hp5343AResolution.Hz100: return "SR5";
                case Hp5343AResolution.kHz1: return "SR6";
                case Hp5343AResolution.kHz10: return "SR7";
                case Hp5343AResolution.kHz100: return "SR8";
                case Hp5343AResolution.MHz1: return "SR9";
                default: throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }

        /// <summary>Sets the frequency resolution (<c>SR{n}</c>).</summary>
        public void SetResolution(Hp5343AResolution resolution) => Send(ResolutionCode(resolution));

        /// <summary>Selects the output mode: <c>ST0</c> none, <c>ST1</c> output-only-if-addressed,
        /// <c>ST2</c> wait-until-addressed (required with HOLD/short gates so a reading is not discarded).</summary>
        public void SetOutputMode(int mode)
        {
            if (mode < 0 || mode > 2) throw new ArgumentOutOfRangeException(nameof(mode), mode, "Output mode is ST0..ST2.");
            Send("ST" + mode.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Reads the measured frequency in Hz (the counter talks a fixed-width
        /// <c>F NNNNN.NNNNNN E 06</c> record; dashes/all-9s/all-0s are error indications and throw).</summary>
        public double ReadFrequency() => ParseFrequency(_session.ReadString());

        /// <summary>Parses a 5343A frequency reading (<c>&lt;sp&gt;F&lt;sp&gt;&lt;sp&gt;NNNNN.NNNNNN E 06</c>,
        /// the mantissa in MHz scaled by <c>E06</c> to Hz); throws on the dashes (over/under-level),
        /// all-9s (display overflow) and all-0s (insufficient signal) indications.</summary>
        internal static double ParseFrequency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 5343A frequency reading.");
            var s = raw.Trim();

            // Strip the leading 'F' tag and the spaces around the 'E' exponent so it parses as a float.
            var compact = s.Replace("F", string.Empty).Replace(" ", string.Empty);

            // Isolate the mantissa (before the exponent) for the error-indication checks.
            var e = compact.IndexOfAny(new[] { 'e', 'E' });
            var mantissa = e >= 0 ? compact.Substring(0, e) : compact;

            // Over/under-level conditions are shown as dashes (a mantissa with no digits).
            if (mantissa.IndexOf('-') >= 0 && mantissa.IndexOfAny("0123456789".ToCharArray()) < 0)
                throw new InvalidOperationException("5343A signalled an over/under-level condition (dashes) — no valid frequency.");

            if (mantissa.StartsWith("99999.999999", StringComparison.Ordinal))
                throw new InvalidOperationException("5343A signalled display overflow (99999.999999E06).");

            if (!double.TryParse(compact, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
            {
                var m = System.Text.RegularExpressions.Regex.Match(compact, @"[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");
                if (!m.Success || !double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out hz))
                    throw new FormatException($"Unrecognized 5343A frequency reading: '{raw}'.");
            }

            if (hz == 0.0)
                throw new InvalidOperationException("5343A signalled insufficient signal (00000.000000E06).");

            return hz;
        }
    }
}
