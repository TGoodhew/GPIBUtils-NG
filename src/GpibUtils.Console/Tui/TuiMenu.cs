using System.Collections.Generic;

namespace GpibUtils.Console.Tui
{
    /// <summary>The top-level screens the interactive TUI can navigate to.</summary>
    public enum TuiScreen
    {
        /// <summary>List providers + capabilities and set the active one.</summary>
        Providers,
        /// <summary>Scan the bus for instruments (extender-aware).</summary>
        Discover,
        /// <summary>Browse the known-instrument catalog and open a query console on one.</summary>
        Instruments,
        /// <summary>Send SCPI to a resource and read replies.</summary>
        Query,
        /// <summary>Leave the interactive UI.</summary>
        Exit,
    }

    /// <summary>One entry on the TUI main menu: the screen it opens plus its label/description.</summary>
    public sealed class TuiMenuItem
    {
        public TuiScreen Screen { get; }
        public string Label { get; }
        public string Description { get; }

        public TuiMenuItem(TuiScreen screen, string label, string description)
        {
            Screen = screen;
            Label = label;
            Description = description;
        }

        public override string ToString() => Label;
    }

    /// <summary>
    /// The main-menu model for the interactive TUI. Pure data (no console dependency) so it can be unit
    /// tested. Every entry maps to a capability that already exists in the one-shot CLI and the WPF app —
    /// the TUI is a new <i>presentation</i> of the shared capability set, per the UI-parity rule (#172).
    /// </summary>
    public static class TuiMenu
    {
        /// <summary>The ordered main-menu entries. Exit is always last.</summary>
        public static IReadOnlyList<TuiMenuItem> Items { get; } = new[]
        {
            new TuiMenuItem(TuiScreen.Providers,   "Providers",     "List GPIB providers + capabilities; set the active provider"),
            new TuiMenuItem(TuiScreen.Discover,    "Discover",      "Scan the bus for instruments (extender-aware)"),
            new TuiMenuItem(TuiScreen.Instruments, "Instruments",   "Browse the known-instrument catalog and open a query console"),
            new TuiMenuItem(TuiScreen.Query,       "Query console", "Send SCPI to a resource and read the reply"),
            new TuiMenuItem(TuiScreen.Exit,        "Exit",          "Leave the interactive UI"),
        };
    }
}
