using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 8350B for use with <see cref="SimulatedGpibProvider"/>. Decodes the
    /// HP-IB mnemonics the driver writes (<c>IP</c> / <c>CW … MZ</c> / <c>PL … DM</c>) back into
    /// frequency / power state so the <see cref="Hp8350B"/> driver can be exercised and asserted with no
    /// hardware.
    /// </summary>
    public sealed class Hp8350BSimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        /// <summary>Last commanded CW frequency in MHz; null after preset / before it is set.</summary>
        public double? FrequencyMHz { get; private set; }

        /// <summary>Last commanded power in dBm; null after preset / before it is set.</summary>
        public double? PowerDbm { get; private set; }

        public Hp8350BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Hewlett-Packard,8350B,0,0",
                WriteObserver = Apply
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            var upper = cmd.ToUpperInvariant();
            if (upper == "IP") { FrequencyMHz = null; PowerDbm = null; return; }
            if (upper.StartsWith("CW")) { FrequencyMHz = ExtractNumber(cmd) ?? FrequencyMHz; return; }
            if (upper.StartsWith("PL")) { PowerDbm = ExtractNumber(cmd) ?? PowerDbm; return; }
        }

        private static double? ExtractNumber(string s)
        {
            foreach (var token in s.Split(' ', '\t'))
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;
            return null;
        }
    }
}
