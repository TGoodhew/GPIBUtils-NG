using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Switches
{
    /// <summary>
    /// Driver for the HP/Agilent 11713A Attenuator/Switch Driver. Converts desired attenuation (dB)
    /// into the 11713A's A/B relay data strings (via <see cref="Hp11713ACommandBuilder"/>), sends them
    /// over any <see cref="IInstrumentSession"/>, drives the independent switches S9/S0, and keeps a
    /// software shadow of state — the 11713A is <b>listen-only</b> and cannot be queried.
    /// </summary>
    public sealed class Hp11713A
    {
        /// <summary>Factory GPIB address of an 11713A (the migrated app's default).</summary>
        public const string DefaultResource = "GPIB0::28::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp11713A(IInstrumentSession session, AttenuatorConfig config)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public AttenuatorConfig Config { get; }
        public DeviceState State { get; } = new DeviceState();

        /// <summary>The resource string this driver's session was opened for.</summary>
        public string ResourceName => _session.ResourceName;

        /// <summary>Every data string sent, in order — the 11713A cannot be read back.</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>
        /// Swap the A/B relay sense. The 11713A manual maps A = insert section, B = bypass
        /// (so 0 dB = all B). If this attenuator is cabled with the opposite sense, set this
        /// true so 0 dB drives the relays the other way.
        /// </summary>
        public bool InvertSense { get; set; }

        public void Initialize()
        {
            _session.Clear();                       // ignored by the listen-only 11713A, but harmless
            SetEngaged(Array.Empty<int>());         // known state: 0 dB (all sections bypassed)
        }

        private string Sense(string command)
        {
            if (!InvertSense) return command;
            var c = command.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] == 'A') c[i] = 'B';
                else if (c[i] == 'B') c[i] = 'A';
            }
            return new string(c);
        }

        private void Send(string command)
        {
            _session.Write(command);
            _history.Add(command);
        }

        /// <summary>
        /// Sets total attenuation across both banks. Returns the data string sent.
        /// Throws <see cref="ArgumentOutOfRangeException"/> if the value is unreachable.
        /// </summary>
        public string SetAttenuationDb(int db)
        {
            var engaged = Hp11713ACommandBuilder.Solve(Config.AllSections.ToList(), db);
            if (engaged == null)
                throw new ArgumentOutOfRangeException(nameof(db),
                    $"{db} dB is not achievable (range 0-{Config.MaxDecibels} dB).");

            string command = Sense(Hp11713ACommandBuilder.BuildString(Config.AllSections, new HashSet<int>(engaged)));
            Send(command);

            State.Engaged.Clear();
            foreach (var d in engaged) State.Engaged.Add(d);
            return command;
        }

        /// <summary>
        /// Engages exactly the given section digits and bypasses all others (across
        /// both banks). Config-independent relay control used for identification.
        /// </summary>
        public string SetEngaged(IEnumerable<int> digits)
        {
            var set = new HashSet<int>(digits);
            string command = Sense(Hp11713ACommandBuilder.BuildString(Config.AllSections, set));
            Send(command);

            State.Engaged.Clear();
            foreach (var d in set) State.Engaged.Add(d);
            return command;
        }

        /// <summary>Sets a single attenuator bank, leaving the other bank unchanged.</summary>
        public string SetBankDb(IReadOnlyList<Section> bank, int db)
        {
            var engaged = Hp11713ACommandBuilder.Solve(bank, db);
            if (engaged == null)
                throw new ArgumentOutOfRangeException(nameof(db),
                    $"{db} dB is not achievable on this bank.");

            string command = Sense(Hp11713ACommandBuilder.BuildString(bank, new HashSet<int>(engaged)));
            Send(command);

            foreach (var s in bank) State.Engaged.Remove(s.Digit);
            foreach (var d in engaged) State.Engaged.Add(d);
            return command;
        }

        /// <summary>Drives independent switch S9 (true = A9, false = B9).</summary>
        public string SetSwitch9(bool on)
        {
            string command = Hp11713ACommandBuilder.Switch9(on);
            Send(command);
            State.Switch9 = on;
            return command;
        }

        /// <summary>Drives independent switch S0 (true = A0, false = B0).</summary>
        public string SetSwitch0(bool on)
        {
            string command = Hp11713ACommandBuilder.Switch0(on);
            Send(command);
            State.Switch0 = on;
            return command;
        }

        /// <summary>Sends a raw data string verbatim (not reflected in tracked state).</summary>
        public void SendRaw(string command) => Send(command);
    }
}
