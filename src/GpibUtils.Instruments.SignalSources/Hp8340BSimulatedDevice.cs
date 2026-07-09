using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.SignalSources
{
    /// <summary>
    /// An in-memory model of an HP 8340B for use with <see cref="SimulatedGpibProvider"/>. It decodes the
    /// HP-IB mnemonics the driver writes (<c>IP</c> / <c>CW … MZ</c> / <c>PL … DB</c> / <c>RF1</c> / <c>RF0</c>)
    /// back into frequency / power / RF state, so the driver can be exercised — and asserted — with no
    /// hardware. Register <see cref="Instrument"/> with the simulated provider, then read the decoded state.
    /// </summary>
    public sealed class Hp8340BSimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        /// <summary>Last commanded CW frequency in MHz; null after preset / before it is set.</summary>
        public double? FrequencyMHz { get; private set; }

        /// <summary>Last commanded power in dBm; null after preset / before it is set.</summary>
        public double? PowerDbm { get; private set; }

        /// <summary>Whether the RF output is enabled (RF1). Preset (IP) leaves it off in this model.</summary>
        public bool RfOn { get; private set; }

        public Hp8340BSimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "Hewlett-Packard,8340B,0,0",
                WriteObserver = Apply
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            var upper = cmd.ToUpperInvariant();

            if (upper == "IP") { FrequencyMHz = null; PowerDbm = null; RfOn = false; return; }
            if (upper == "RF1") { RfOn = true; return; }
            if (upper == "RF0") { RfOn = false; return; }
            if (upper.StartsWith("CW")) { FrequencyMHz = ExtractNumber(cmd) ?? FrequencyMHz; return; }
            if (upper.StartsWith("PL")) { PowerDbm = ExtractNumber(cmd) ?? PowerDbm; return; }
        }

        // Pulls the first parseable numeric token out of a "CW <val> MZ" / "PL <val> DB" message.
        private static double? ExtractNumber(string s)
        {
            foreach (var token in s.Split(' ', '\t'))
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;
            return null;
        }
    }
}
