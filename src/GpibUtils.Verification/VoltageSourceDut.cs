using System;
using GpibUtils.Instruments.Calibrators;
using GpibUtils.Instruments.PowerSupplies;

namespace GpibUtils.Verification
{
    /// <summary>
    /// A device under test that sources a programmed DC voltage and can gate its output — a DC calibrator
    /// (Fluke 5440A) or a bench power supply (E3633A, DP832, 6625A). Lets <see cref="DcSourceVerifier"/>
    /// drive either kind uniformly while a DMM reference reads back the true output.
    /// </summary>
    public interface IVoltageSourceDut
    {
        /// <summary>Human-readable name of the DUT.</summary>
        string DisplayName { get; }

        /// <summary>Programs the output voltage (volts).</summary>
        void SetVolts(double volts);

        /// <summary>Enables the output (Operate / output-on).</summary>
        void EnableOutput();

        /// <summary>Disables the output (Standby / output-off).</summary>
        void DisableOutput();
    }

    /// <summary>Adapts a DC voltage calibrator (<see cref="IDcVoltageCalibrator"/>) as a voltage-source DUT.</summary>
    public sealed class CalibratorVoltageDut : IVoltageSourceDut
    {
        private readonly IDcVoltageCalibrator _cal;

        public CalibratorVoltageDut(IDcVoltageCalibrator calibrator, string displayName = null)
        {
            _cal = calibrator ?? throw new ArgumentNullException(nameof(calibrator));
            DisplayName = displayName ?? "DC voltage calibrator";
        }

        public string DisplayName { get; }
        public void SetVolts(double volts) => _cal.SetOutputVolts(volts);
        public void EnableOutput() => _cal.SetOutputState(CalibratorOutputState.Operate);
        public void DisableOutput() => _cal.SetOutputState(CalibratorOutputState.Standby);
    }

    /// <summary>Adapts a DC power supply (<see cref="IDcPowerSupply"/>) as a voltage-source DUT.</summary>
    public sealed class PowerSupplyVoltageDut : IVoltageSourceDut
    {
        private readonly IDcPowerSupply _psu;

        public PowerSupplyVoltageDut(IDcPowerSupply supply, string displayName = null)
        {
            _psu = supply ?? throw new ArgumentNullException(nameof(supply));
            DisplayName = displayName ?? "DC power supply";
        }

        public string DisplayName { get; }
        public void SetVolts(double volts) => _psu.SetVoltage(volts);
        public void EnableOutput() => _psu.SetOutput(true);
        public void DisableOutput() => _psu.SetOutput(false);
    }
}
