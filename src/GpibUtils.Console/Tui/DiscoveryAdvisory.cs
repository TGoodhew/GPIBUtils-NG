namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// Interpreting a discovery scan on the bench. GPIB scans are untrustworthy when an HP-IB bus extender
    /// (HP 37204A or similar) is in the path: the extender ACKs the address handshake for its whole remote
    /// segment, so a VISA scan reports nearly every address 0–30 as present — all phantom. When that many
    /// resources come back, the UI steers the user to explicit addressing instead. Pure logic (no console)
    /// so the threshold is unit-testable and shared by the TUI's Discover screen.
    /// </summary>
    public static class DiscoveryAdvisory
    {
        /// <summary>At/above this many discovered resources, treat the list as an extender phantom scan.</summary>
        public const int ExtenderPhantomThreshold = 15;

        /// <summary>Whether <paramref name="discoveredCount"/> looks like an extender phantom list.</summary>
        public static bool IsExtenderPhantom(int discoveredCount) => discoveredCount >= ExtenderPhantomThreshold;
    }
}
