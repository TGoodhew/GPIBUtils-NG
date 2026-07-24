using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Meters
{
    /// <summary>A measurement function of the HP 3458A, mapped to its native <c>FUNC</c> keyword.</summary>
    public enum Hp3458AFunction
    {
        /// <summary>DC voltage — <c>FUNC DCV</c>.</summary>
        DcVoltage,
        /// <summary>AC voltage — <c>FUNC ACV</c> (uses <c>SETACV SYNC</c>).</summary>
        AcVoltage,
        /// <summary>2-wire resistance — <c>FUNC OHM</c>.</summary>
        Resistance2Wire,
        /// <summary>4-wire resistance — <c>FUNC OHMF</c>.</summary>
        Resistance4Wire,
        /// <summary>DC current — <c>FUNC DCI</c>.</summary>
        DcCurrent,
        /// <summary>AC current — <c>FUNC ACI</c>.</summary>
        AcCurrent,
        /// <summary>Frequency — <c>FUNC FREQ</c>.</summary>
        Frequency,
        /// <summary>Period — <c>FUNC PER</c>.</summary>
        Period
    }

    /// <summary>
    /// Driver for the HP/Agilent 3458A 8½-digit DMM — driven with its own (non-SCPI) command language
    /// (<c>RESET</c>, <c>FUNC</c>, <c>NPLC</c>, <c>RES</c>, <c>SETACV</c>, <c>TARM</c>). Configures a
    /// function, then takes single triggered readings or a burst. Ported from the <c>HP3458ACapture</c>
    /// AC-volts logging app (issue #31); runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp3458A
    {
        /// <summary>GPIB address of the 3458A — its documented factory-default GPIB address is 22 (3458A
        /// User's Guide: "The multimeter leaves the factory with the address set to decimal 22"). Override
        /// with <c>--address</c>. Matches the legacy capture app's hardcoded resource.</summary>
        public const string DefaultResource = "GPIB0::22::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp3458A(IInstrumentSession session) =>
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

        /// <summary>The 3458A answers <c>ID?</c> with its model (e.g. "HP3458A"); it has no <c>*IDN?</c>.</summary>
        public string Identify() => Query("ID?");

        public void Initialize()
        {
            _session.Clear();
            Send("RESET");         // preset to the power-on state
            Send("END ALWAYS");    // assert EOI after every reading (clean host reads)
        }

        public void Reset() => Send("RESET");

        internal static string FuncKeyword(Hp3458AFunction function)
        {
            switch (function)
            {
                case Hp3458AFunction.DcVoltage: return "DCV";
                case Hp3458AFunction.AcVoltage: return "ACV";
                case Hp3458AFunction.Resistance2Wire: return "OHM";
                case Hp3458AFunction.Resistance4Wire: return "OHMF";
                case Hp3458AFunction.DcCurrent: return "DCI";
                case Hp3458AFunction.AcCurrent: return "ACI";
                case Hp3458AFunction.Frequency: return "FREQ";
                case Hp3458AFunction.Period: return "PER";
                default: throw new ArgumentOutOfRangeException(nameof(function), function, null);
            }
        }

        /// <summary>Selects a measurement function (<c>FUNC …</c>); AC volts also sends <c>SETACV SYNC</c>
        /// (synchronous conversion, as the capture app did).</summary>
        public void ConfigureFunction(Hp3458AFunction function)
        {
            Send("FUNC " + FuncKeyword(function));
            if (function == Hp3458AFunction.AcVoltage)
                Send("SETACV SYNC");
        }

        /// <summary>Sets the integration time in power-line cycles (<c>NPLC …</c>).</summary>
        public void SetNplc(double nplc) => Send("NPLC " + nplc.ToString("G7", CultureInfo.InvariantCulture));

        /// <summary>Sets the measurement resolution as a percent of range (<c>RES …</c>), e.g. 0.001.</summary>
        public void SetResolution(double percentOfRange) =>
            Send("RES " + percentOfRange.ToString("G7", CultureInfo.InvariantCulture));

        /// <summary>Triggers a single reading (<c>TARM SGL</c>) and returns it.</summary>
        public double ReadValue() => ParseReading(Query("TARM SGL"));

        /// <summary>Triggers and reads <paramref name="count"/> single readings.</summary>
        public double[] ReadValues(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be >= 1.");
            var values = new double[count];
            for (int i = 0; i < count; i++) values[i] = ReadValue();
            return values;
        }

        /// <summary>Parses a 3458A reading. The SCPI ±9.9E37 over-range / NaN sentinel is rejected
        /// (<see cref="InvalidOperationException"/>), not returned.</summary>
        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new FormatException("Empty 3458A reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 3458A reading: '{raw}'.");
            return ScpiReading.Guard(v, raw.Trim(), "3458A");
        }
    }
}
