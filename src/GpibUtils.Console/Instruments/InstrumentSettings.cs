using System.ComponentModel;
using GpibUtils.Visa;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>
    /// Shared global options for every instrument command branch (issue #45): which provider, which
    /// address, and the I/O timeout. Each device branch extends this so the same three options appear
    /// — with self-documenting <c>--help</c> — at every level of the command tree.
    /// </summary>
    public class InstrumentSettings : ProviderSettings
    {
        [CommandOption("-a|--address <RESOURCE>")]
        [Description("VISA resource string of the instrument (e.g. GPIB0::28::INSTR). Defaults per device.")]
        public string Address { get; set; }

        [CommandOption("-t|--timeout <MS>")]
        [Description("I/O timeout in milliseconds (default 5000).")]
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>Opens a session to <see cref="Address"/> (or <paramref name="defaultAddress"/>) on the
        /// resolved provider, applying <see cref="TimeoutMs"/>.</summary>
        internal IInstrumentSession OpenSession(string defaultAddress)
        {
            var provider = Resolve();
            var resource = string.IsNullOrWhiteSpace(Address) ? defaultAddress : Address;
            return provider.Open(resource, new SessionSettings { TimeoutMilliseconds = TimeoutMs });
        }
    }
}
