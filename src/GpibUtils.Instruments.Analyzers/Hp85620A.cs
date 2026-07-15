using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>Which mass-storage device the 85620A commands act on.</summary>
    public enum MassStorageDevice
    {
        /// <summary>The 85620A module's internal battery-backed RAM (<c>MSDEV MEM</c>).</summary>
        Module,

        /// <summary>The removable memory card / FRAM in the module (<c>MSDEV CARD</c>).</summary>
        Card
    }

    /// <summary>A parsed mass-memory catalog: the stored entry names and the free-space report.</summary>
    public struct CatalogResult
    {
        /// <summary>Names of the entries (DLPs / state files) stored on the selected device.</summary>
        public string[] Entries { get; set; }

        /// <summary>Free bytes reported by the trailing <c>BYTES FREE n</c> line, or -1 if not present.</summary>
        public long BytesFree { get; set; }

        /// <summary>The raw catalog response.</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// Driver for the HP 85620A Mass Memory Module — a memory-card / DLP-storage module that plugs into an
    /// 8560-series analyzer (e.g. the 8563E) and is driven through the analyzer's HP-IB mass-storage command
    /// set. Selects the storage device (module RAM vs card), catalogs it, stores/loads named entries between
    /// module memory and the card, disposes the module contents, and downloads Downloadable Programs (DLPs)
    /// via <c>FUNCDEF</c>. Ported from the <c>MemCardTest</c> (8563E + FRAM card) and <c>DLPBits</c> (DLP
    /// loader) apps (issues #10 / #14). Runs over any <see cref="IInstrumentSession"/>.
    ///
    /// <para><b>Completion:</b> the module uses the analyzer's <c>DONE?;</c> / <c>ERR?;</c> handshake, NOT
    /// SRQ — a store/load holds off the <c>DONE?;</c> response until the operation commits, so the blocking
    /// read IS the wait, and <c>ERR?;</c> then surfaces any analyzer-side rejection (full card, name clash).
    /// The #43 SRQ engine is deliberately not used here.</para>
    ///
    /// <para>The module's raw SRAM-image decode (a bit de-scramble + DLP extraction from a binary dump) and
    /// card FORMAT (which cannot be done over HP-IB) are out of scope for the live driver — see the deferred
    /// follow-up noted on #14.</para>
    /// </summary>
    public sealed class Hp85620A
    {
        /// <summary>GPIB address of the host analyzer (the 85620A is addressed through it) — the 8563E/8560-series
        /// factory-default HP-IB address is 18 (matches the MemCardTest source). Override with <c>--address</c>.
        /// Never trust bus-scan discovery on this bench (HP-IB extenders make every address look present).</summary>
        public const string DefaultResource = "GPIB0::18::INSTR";

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        public Hp85620A(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every command sent through the driver, in order (for CLI echo / tests).</summary>
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

        private static string Code(MassStorageDevice device) => device == MassStorageDevice.Card ? "CARD" : "MEM";

        public string Identify() => Query("ID?");

        /// <summary>Device clear — flush any stale data left in the analyzer's output buffer.</summary>
        public void Initialize() => _session.Clear();

        /// <summary>Selects the active mass-storage device (<c>MSDEV MEM|CARD;</c>).</summary>
        public void SelectDevice(MassStorageDevice device) => Send("MSDEV " + Code(device) + ";");

        /// <summary>
        /// Reads the raw directory listing of <paramref name="device"/> (<c>MSDEV &lt;dev&gt;; CATALOG?;</c>).
        /// A device clear first flushes any stale buffered response so we don't read leftovers.
        /// </summary>
        public string ReadCatalogRaw(MassStorageDevice device)
        {
            _session.Clear();
            Send("MSDEV " + Code(device) + ";");
            return Query("CATALOG?;");
        }

        /// <summary>Reads and parses the catalog of <paramref name="device"/> into entry names + free bytes.</summary>
        public CatalogResult Catalog(MassStorageDevice device) => ParseCatalog(ReadCatalogRaw(device));

        /// <summary>
        /// Stores a named module-memory entry onto the card (<c>CARDSTORE %name%;</c>), waits for the
        /// analyzer to commit (<c>DONE?;</c>), and checks <c>ERR?;</c>. Throws <see cref="Hp85620AException"/>
        /// on a non-zero error.
        /// </summary>
        public void StoreToCard(string name)
        {
            ValidateName(name);
            Send("CARDSTORE %" + name + "%;");
            WaitDone();
            int err = GetError();
            if (err != 0) throw Hp85620AException.FromErr("CARDSTORE", name, err);
        }

        /// <summary>
        /// Loads a named entry from the card back into module memory (<c>CARDLOAD %name%;</c>), waits for
        /// completion (<c>DONE?;</c>), and checks <c>ERR?;</c>. Throws on a non-zero error.
        /// </summary>
        public void LoadFromCard(string name)
        {
            ValidateName(name);
            Send("CARDLOAD %" + name + "%;");
            WaitDone();
            int err = GetError();
            if (err != 0) throw Hp85620AException.FromErr("CARDLOAD", name, err);
        }

        /// <summary>Disposes all entries in the module's memory (<c>MSDEV MEM; DISPOSE ALL;</c>), then waits
        /// for completion. Card FORMAT cannot be done over HP-IB.</summary>
        public void ClearModule()
        {
            Send("MSDEV MEM;");
            Send("DISPOSE ALL;");
            WaitDone();
        }

        /// <summary>
        /// Downloads a Downloadable Program definition (<c>FUNCDEF &lt;definition&gt;;</c>) and checks
        /// <c>ERR?;</c>. The <paramref name="definition"/> is the DLP body as extracted from an SRAM image
        /// (name + program text). Throws on a non-zero error.
        /// </summary>
        public void DefineFunction(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition))
                throw new ArgumentException("A DLP definition is required.", nameof(definition));
            Send("FUNCDEF " + definition + ";");
            int err = GetError();
            if (err != 0) throw Hp85620AException.FromErr("FUNCDEF", definition, err);
        }

        /// <summary>Issues the blocking completion query (<c>DONE?;</c>) — the analyzer holds off its reply
        /// until the prior operation commits. Returns true if it reported done ("1").</summary>
        public bool WaitDone() => Query("DONE?;").TrimStart('+') == "1";

        /// <summary>Reads the analyzer error code (<c>ERR?;</c>). 0 = no error.</summary>
        public int GetError() => ParseInt(Query("ERR?;"));

        // ---- parsing --------------------------------------------------------

        /// <summary>Parses a catalog response of the form
        /// <c>&lt;entry&gt;,&lt;entry&gt;,…&lt;LF&gt;BYTES FREE n</c> into names + free bytes.</summary>
        internal static CatalogResult ParseCatalog(string raw)
        {
            var result = new CatalogResult { Raw = raw, Entries = Array.Empty<string>(), BytesFree = -1 };
            if (string.IsNullOrWhiteSpace(raw)) return result;

            long bytesFree = -1;
            string entriesText = raw;
            var m = Regex.Match(raw, @"BYTES\s+FREE\s+([0-9]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                bytesFree = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                entriesText = raw.Substring(0, m.Index);
            }

            var entries = new List<string>();
            foreach (var token in entriesText.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length > 0) entries.Add(t);
            }

            result.Entries = entries.ToArray();
            result.BytesFree = bytesFree;
            return result;
        }

        internal static int ParseInt(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (s.Length == 0) return 0;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return (int)d;
            throw new FormatException($"Unrecognized 85620A numeric response: '{raw}'.");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An entry name is required.", nameof(name));
            if (name.IndexOf('%') >= 0)
                throw new ArgumentException("Entry name must not contain '%' (the CARDSTORE/CARDLOAD delimiter).", nameof(name));
        }
    }
}
