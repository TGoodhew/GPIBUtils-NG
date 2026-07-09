using System.Linq;
using GpibUtils.Wpf.ViewModels;
using Xunit;

namespace GpibUtils.Wpf.Tests
{
    // Exercises the shell's view-model logic headlessly (no window) — it sits on the shared
    // GpibProviders registry, so it works with the Simulated provider and no hardware.
    public class MainViewModelTests
    {
        [Fact]
        public void Ctor_loads_providers_from_the_registry()
        {
            var vm = new MainViewModel();

            Assert.NotEmpty(vm.Providers);
            Assert.Contains(vm.ProviderNames, n => n == "Simulated");
            Assert.Contains(vm.ProviderNames, n => n == "NI-VISA"); // registered even as an unavailable stub
            Assert.False(string.IsNullOrWhiteSpace(vm.SelectedProvider));
            Assert.Contains(vm.SelectedProvider, vm.ProviderNames);
        }

        [Fact]
        public void Query_over_simulated_returns_a_reply()
        {
            var vm = new MainViewModel
            {
                SelectedProvider = "Simulated",
                Resource = "GPIB0::5::INSTR",
                Command = "*IDN?"
            };

            vm.QueryCommand.Execute(null);

            Assert.Contains("Simulated Instrument", vm.Reply);
            Assert.DoesNotContain("Error", vm.Status);
        }

        [Fact]
        public void Query_can_execute_requires_provider_and_resource()
        {
            var vm = new MainViewModel { SelectedProvider = "Simulated", Resource = "" };
            Assert.False(vm.QueryCommand.CanExecute(null));

            vm.Resource = "GPIB0::5::INSTR";
            Assert.True(vm.QueryCommand.CanExecute(null));
        }

        [Fact]
        public void Failed_query_reports_error_in_status_without_throwing()
        {
            var vm = new MainViewModel
            {
                SelectedProvider = "No-Such-Provider",
                Resource = "GPIB0::9::INSTR",
                Command = "*IDN?"
            };

            // The guard must turn a resolution/IO failure into a status message, not an exception.
            vm.QueryCommand.Execute(null);
            Assert.StartsWith("Error:", vm.Status);
        }
    }
}
