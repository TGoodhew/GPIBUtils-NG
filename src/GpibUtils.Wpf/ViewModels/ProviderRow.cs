using System;
using System.Collections.Generic;
using System.Linq;
using GpibUtils.Visa;

namespace GpibUtils.Wpf.ViewModels
{
    /// <summary>A read-only row describing one registered provider, for the providers grid.</summary>
    public sealed class ProviderRow
    {
        public string Name { get; }
        public string Available { get; }
        public string Default { get; }
        public string Capabilities { get; }
        public string Detail { get; }

        public ProviderRow(IGpibProvider provider, bool isDefault)
        {
            Name = provider.Name;
            Available = provider.IsAvailable ? "yes" : "no";
            Default = isDefault ? "★" : string.Empty;
            Capabilities = Describe(provider.Capabilities);
            Detail = provider.IsAvailable ? string.Empty : (provider.UnavailableReason ?? string.Empty);
        }

        private static string Describe(ProviderCapabilities c)
        {
            var parts = new List<string>();
            if (c.Discovery) parts.Add("discover");
            if (c.SerialPoll) parts.Add("serial-poll");
            if (c.ServiceRequest) parts.Add("srq");
            if (c.DeviceClear) parts.Add("clear");
            if (c.ReturnToLocal) parts.Add("local");
            if (c.NativeAddressing) parts.Add("native");
            return string.Join(", ", parts);
        }
    }
}
