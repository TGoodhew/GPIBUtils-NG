using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.PowerSupplies
{
    /// <summary>
    /// Driver for the HP/Agilent 6625A System DC Power Supply — a multi-output precision supply with a
    /// channel-scoped mnemonic HP-IB command set (<c>VSET</c>/<c>ISET</c>/<c>OUT</c>/<c>VOUT?</c>/<c>IOUT?</c>).
    /// Implements <see cref="IDcPowerSupply"/> on the active <see cref="SelectedChannel"/> (1 or 2), the
    /// RigolDp832 multi-output pattern. Reconstructed from the 6625A Operating Manual, Chapter 5 (issue #117).
    /// Runs over any <see cref="IInstrumentSession"/>.
    /// </summary>
    public sealed class Hp6625A : IDcPowerSupply
    {
        /// <summary>GPIB address of the 6625A — HP system-supply default 5. Override with <c>--address</c>.</summary>
        public const string DefaultResource = "GPIB0::5::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp6625A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;
        public IReadOnlyList<string> History => _history;

        /// <summary>The output (1 or 2) the <see cref="IDcPowerSupply"/> methods act on.</summary>
        public int SelectedChannel { get; set; } = 1;
        private int Ch => SelectedChannel == 2 ? 2 : 1;

        private void Send(string command) { _session.Write(command); _history.Add(command); }
        private string Query(string command) { _history.Add(command); return (_session.Query(command) ?? string.Empty).Trim(); }

        /// <summary>Identifies the supply (<c>ID?</c> — the 6625A predates <c>*IDN?</c>).</summary>
        public string Identify() => Query("ID?");

        public void Initialize() { _session.Clear(); Send("CLR"); }
        public void Reset() => Send("CLR");

        public void SetVoltage(double volts) =>
            Send("VSET " + Ch + "," + volts.ToString("0.####", CultureInfo.InvariantCulture));

        public void SetCurrentLimit(double amps) =>
            Send("ISET " + Ch + "," + amps.ToString("0.####", CultureInfo.InvariantCulture));

        public void SetOutput(bool on) => Send("OUT " + Ch + "," + (on ? "1" : "0"));

        public double MeasureVoltage() => ParseReading(Query("VOUT? " + Ch));
        public double MeasureCurrent() => ParseReading(Query("IOUT? " + Ch));

        internal static double ParseReading(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty 6625A reading.");
            if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                throw new FormatException($"Unrecognized 6625A reading: '{raw}'.");
            return v;
        }
    }
}
