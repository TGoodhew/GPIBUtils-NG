using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// Mutable state for one interactive TUI run: the active provider and I/O timeout the screens open
    /// sessions with. Mirrors the CLI's <c>--provider</c> / <c>--timeout</c> options — the TUI just lets
    /// you set them once and reuse them, instead of passing them on every command (the accepted invocation
    /// difference under the UI-parity rule).
    /// </summary>
    internal sealed class TuiSession
    {
        /// <summary>Active provider name (defaults to a sensible available provider — see <see cref="PickInitial"/>).</summary>
        public string ProviderName { get; set; }

        /// <summary>I/O timeout applied to opened sessions, in milliseconds.</summary>
        public int TimeoutMs { get; set; } = 5000;

        public TuiSession(string providerName = null)
        {
            ProviderName = string.IsNullOrWhiteSpace(providerName) ? PickInitial() : providerName.Trim();
        }

        /// <summary>Resolves the active provider from the registry (throws if the name is unknown).</summary>
        public IGpibProvider ResolveProvider() =>
            string.IsNullOrWhiteSpace(ProviderName) ? GpibProviders.Default : GpibProviders.Get(ProviderName);

        /// <summary>Opens a session on the active provider at <paramref name="resource"/> with the timeout.</summary>
        public IInstrumentSession Open(string resource) =>
            ResolveProvider().Open(resource, new SessionSettings { TimeoutMilliseconds = TimeoutMs });

        /// <summary>The registry default provider when it is available; otherwise the first available
        /// provider (so the UI is usable out of the box on a machine with no NI-VISA); otherwise the
        /// registry default name regardless.</summary>
        public static string PickInitial()
        {
            var defaultName = GpibProviders.DefaultProviderName;
            var providers = GpibProviders.All;

            var def = providers.FirstOrDefault(p =>
                string.Equals(p.Name, defaultName, System.StringComparison.OrdinalIgnoreCase));
            if (def != null && def.IsAvailable) return def.Name;

            var firstAvailable = providers.FirstOrDefault(p => p.IsAvailable);
            return firstAvailable?.Name ?? defaultName;
        }
    }
}
