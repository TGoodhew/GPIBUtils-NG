using System.ComponentModel;
using GpibUtils.Common;
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

        /// <summary>
        /// Opens a session on the resolved provider, applying <see cref="TimeoutMs"/>. The address is
        /// resolved with the precedence <b>--address &gt; configured &gt; default</b>: an explicit
        /// <see cref="Address"/> wins; otherwise a stored override for <paramref name="deviceKey"/> in the
        /// <see cref="InstrumentAddressStore"/> (issue #54); otherwise the driver's
        /// <paramref name="defaultAddress"/> (its manual factory default).
        /// </summary>
        internal IInstrumentSession OpenSession(string deviceKey, string defaultAddress)
        {
            var provider = Resolve();
            var resource = InstrumentAddressStore.Load().Resolve(Address, deviceKey, defaultAddress);
            return provider.Open(resource, new SessionSettings { TimeoutMilliseconds = TimeoutMs });
        }
    }
}
