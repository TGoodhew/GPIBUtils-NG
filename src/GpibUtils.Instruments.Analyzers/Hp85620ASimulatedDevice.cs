using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// An in-memory model of an HP 85620A mass-memory module (as driven through an 8563E) for use with
    /// <see cref="SimulatedGpibProvider"/>, rich enough to drive the <see cref="Hp85620A"/> driver end to end
    /// with no hardware. It models the two storage devices (module RAM + card) as name sets, the active-device
    /// selection (<c>MSDEV</c>), and the store/load/dispose/FUNCDEF operations, and answers <c>ID?</c>,
    /// <c>CATALOG?;</c>, <c>DONE?;</c>, and <c>ERR?;</c>.
    /// </summary>
    public sealed class Hp85620ASimulatedDevice
    {
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();
        private readonly HashSet<string> _module = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _card = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _functions = new List<string>();

        private string _device = "MEM";
        private int _pendingError;

        /// <summary>Every command the analyzer was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Entry names currently in module memory.</summary>
        public IReadOnlyCollection<string> ModuleEntries => _module;

        /// <summary>Entry names currently on the card.</summary>
        public IReadOnlyCollection<string> CardEntries => _card;

        /// <summary>DLP definitions received via FUNCDEF.</summary>
        public IReadOnlyList<string> Functions => _functions;

        /// <summary>Free bytes reported by CATALOG? for each device.</summary>
        public long BytesFree { get; set; } = 128000;

        /// <summary>The error code the next <c>ERR?;</c> returns (and clears). Default 0 = no error.</summary>
        public int PendingError { get => _pendingError; set => _pendingError = value; }

        public Hp85620ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "HP8563E",
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        /// <summary>Seeds an entry into module memory (test fixture helper).</summary>
        public void AddModuleEntry(string name) => _module.Add(name);

        /// <summary>Seeds an entry onto the card (test fixture helper).</summary>
        public void AddCardEntry(string name) => _card.Add(name);

        private void Apply(string command)
        {
            var raw = command.Trim();
            if (raw.Length == 0) return;
            foreach (var part in raw.Split(';'))
            {
                var cmd = part.Trim();
                if (cmd.Length == 0) continue;
                _commands.Add(cmd);
                var upper = cmd.ToUpperInvariant();
                if (upper.EndsWith("?")) continue;   // queries carry no state change

                if (upper.StartsWith("MSDEV"))
                {
                    _device = upper.Contains("CARD") ? "CARD" : "MEM";
                    continue;
                }
                if (upper.StartsWith("CARDSTORE"))
                {
                    var name = ExtractPercentName(cmd);
                    if (name != null) _card.Add(name);   // module -> card
                    continue;
                }
                if (upper.StartsWith("CARDLOAD"))
                {
                    var name = ExtractPercentName(cmd);
                    if (name != null) _module.Add(name);  // card -> module
                    continue;
                }
                if (upper.StartsWith("DISPOSE"))
                {
                    if (_device == "MEM") _module.Clear(); else _card.Clear();
                    continue;
                }
                if (upper.StartsWith("FUNCDEF"))
                {
                    _functions.Add(cmd.Substring("FUNCDEF".Length).Trim());
                    continue;
                }
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant().TrimEnd(';');
            switch (upper)
            {
                case "ID?": return Instrument.IdentificationString;
                case "DONE?": return "1";
                case "ERR?":
                    var e = _pendingError; _pendingError = 0;
                    return e.ToString(CultureInfo.InvariantCulture);
                case "CATALOG?":
                    var names = (_device == "CARD" ? _card : _module).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
                    return string.Join(",", names) + "\nBYTES FREE " + BytesFree.ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static string ExtractPercentName(string command)
        {
            var m = Regex.Match(command, "%([^%]+)%");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
