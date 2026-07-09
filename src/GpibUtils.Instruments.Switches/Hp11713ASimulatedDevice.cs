using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Switches
{
    /// <summary>
    /// An in-memory model of an HP 11713A for use with <see cref="SimulatedGpibProvider"/>. Because the
    /// 11713A is listen-only, it is driven purely by the A/B data strings written to it; this device
    /// decodes those strings back into relay/switch state so drivers can be exercised — and asserted —
    /// with no hardware. Register <see cref="Instrument"/> with the simulated provider, then read the
    /// decoded <see cref="Engaged"/> / <see cref="Switch9"/> / <see cref="Switch0"/> state.
    /// </summary>
    public sealed class Hp11713ASimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        /// <summary>Digits (1-8) of attenuator sections the last commands left engaged (A).</summary>
        public HashSet<int> Engaged { get; } = new HashSet<int>();

        /// <summary>S9 switch: true = A9, false = B9, null = never addressed.</summary>
        public bool? Switch9 { get; private set; }

        /// <summary>S0 switch: true = A0, false = B0, null = never addressed.</summary>
        public bool? Switch0 { get; private set; }

        public Hp11713ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                // The real 11713A cannot talk; this identity is a convenience for the simulator only.
                IdentificationString = "Hewlett-Packard,11713A,0,0",
                WriteObserver = Apply
            };
        }

        /// <summary>Total attenuation the decoded relay state represents, for the given wiring.</summary>
        public int TotalDecibels(AttenuatorConfig config) =>
            config.AllSections.Where(s => Engaged.Contains(s.Digit)).Sum(s => s.Decibels);

        // Decodes an 11713A data string: 'A'/'a' select the ON (engage) field, 'B'/'b' the OFF (bypass)
        // field, and each following digit is a relay. Digit 9 -> S9, 0 -> S0, 1-8 -> attenuator sections.
        // Only the relays named in the string change; others hold their state.
        private void Apply(string command)
        {
            bool engage = true; // A/B prefix required before any digit on a real 11713A
            foreach (var ch in command)
            {
                if (ch == 'A' || ch == 'a') { engage = true; continue; }
                if (ch == 'B' || ch == 'b') { engage = false; continue; }
                if (ch < '0' || ch > '9') continue; // ignore whitespace/other

                int digit = ch - '0';
                if (digit == 9) Switch9 = engage;
                else if (digit == 0) Switch0 = engage;
                else if (engage) Engaged.Add(digit);
                else Engaged.Remove(digit);
            }
        }
    }
}
