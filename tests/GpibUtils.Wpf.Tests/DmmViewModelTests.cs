using GpibUtils.Instruments.Meters;
using GpibUtils.Wpf.ViewModels;
using Xunit;

namespace GpibUtils.Wpf.Tests
{
    /// <summary>Tests for the DMM tab view model's headless core (issue #172, DMM increment): function
    /// mapping, unit selection, and a single read against the Simulated provider.</summary>
    public class DmmViewModelTests
    {
        [Theory]
        [InlineData("dcv", MeasurementFunction.DcVoltage)]
        [InlineData("acv", MeasurementFunction.AcVoltage)]
        [InlineData("res", MeasurementFunction.Resistance2Wire)]
        [InlineData("fres", MeasurementFunction.Resistance4Wire)]
        [InlineData("freq", MeasurementFunction.Frequency)]
        [InlineData("per", MeasurementFunction.Period)]
        public void ResolveFunction_maps_friendly_names(string name, MeasurementFunction expected)
        {
            var vm = new DmmViewModel { SelectedFunction = name };
            Assert.Equal(expected, vm.ResolveFunction());
        }

        [Fact]
        public void ResolveFunction_defaults_to_dc_voltage_for_unknown()
        {
            var vm = new DmmViewModel { SelectedFunction = "bogus" };
            Assert.Equal(MeasurementFunction.DcVoltage, vm.ResolveFunction());
        }

        [Theory]
        [InlineData(MeasurementFunction.DcVoltage, "V")]
        [InlineData(MeasurementFunction.DcCurrent, "A")]
        [InlineData(MeasurementFunction.Resistance2Wire, "Ω")]
        [InlineData(MeasurementFunction.Frequency, "Hz")]
        [InlineData(MeasurementFunction.Period, "s")]
        public void UnitFor_returns_expected_unit(MeasurementFunction fn, string unit)
        {
            Assert.Equal(unit, DmmViewModel.UnitFor(fn));
        }

        [Fact]
        public void ReadOnce_against_simulated_updates_last_value()
        {
            var vm = new DmmViewModel
            {
                SelectedProvider = "Simulated",
                SelectedFunction = "dcv",
            };

            vm.ReadOnceCommand.Execute(null);

            Assert.False(vm.IsMonitoring);
            Assert.NotEqual("—", vm.LastValue);
            Assert.Contains("Read", vm.Status);
        }
    }
}
