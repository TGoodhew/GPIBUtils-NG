using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa;

namespace GpibUtils.Instruments.ElectronicLoads
{
    /// <summary>
    /// Driver for the Maynuo M9811 programmable DC electronic load (and the compatible M97xx family) — a
    /// <b>Modbus RTU</b> device over serial (RS-232/RS-485/USB), not SCPI/GPIB. Puts the load in remote control,
    /// selects a regulation mode (CC/CV/CR/CW) with its setpoint, switches the input on/off, and reads back the
    /// terminal voltage/current. Implements <see cref="IElectronicLoad"/> (issue #164). Runs over any
    /// <see cref="IInstrumentSession"/> whose provider can carry raw bytes (a serial/ASRL resource).
    ///
    /// <para><b>Reconstructed from the M97xx Modbus manual</b> (the file supplied as <c>M9811.pdf</c> actually
    /// documents the M971x family; confirm the M9811's register map and default slave address at the bench).
    /// Setpoints are float32 big-endian across two registers; the CMD register selects mode and load on/off.</para>
    /// </summary>
    public sealed class MaynuoM9811 : IElectronicLoad
    {
        /// <summary>Default resource — a VISA serial (ASRL) resource, since the load is RS-232/USB, not GPIB.
        /// Override with <c>--address</c> for the bench's actual COM/USB resource.</summary>
        public const string DefaultResource = "ASRL1::INSTR";

        // Coils (function 0x05 / 0x01)
        private const ushort CoilRemote = 0x0500;   // PC1: 1 = remote control (front panel disabled)
        // Holding registers (function 0x03 read / 0x10 write); setpoints are float32 (two registers).
        private const ushort RegCmd = 0x0A00;   // u16 command register (mode / input on-off)
        private const ushort RegCurrent = 0x0A01;   // IFIX (CC setpoint, A)
        private const ushort RegVoltage = 0x0A03;   // UFIX (CV setpoint, V)
        private const ushort RegPower = 0x0A05;   // PFIX (CW setpoint, W)
        private const ushort RegResistance = 0x0A07;   // RFIX (CR setpoint, Ω)
        private const ushort RegMeasVoltage = 0x0B00;   // U (measured, V)
        private const ushort RegMeasCurrent = 0x0B02;   // I (measured, A)
        private const ushort RegModel = 0x0B06;   // MODEL (u16) + EDITION (u16)

        // CMD register values
        private const ushort CmdCc = 1, CmdCv = 2, CmdCw = 3, CmdCr = 4, CmdInputOn = 42, CmdInputOff = 43;

        private readonly IInstrumentSession _session;
        private readonly List<string> _history = new List<string>();

        /// <summary>Modbus slave address (default 1). Set from the bench unit's configured address.</summary>
        public byte SlaveAddress { get; set; } = 1;

        public MaynuoM9811(IInstrumentSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public string ResourceName => _session.ResourceName;

        /// <summary>Every Modbus request frame sent, as hex (for CLI echo / tests).</summary>
        public IReadOnlyList<string> History => _history;

        private byte[] Transact(byte[] request)
        {
            _history.Add(ToHex(request));
            _session.WriteBytes(request);
            var resp = _session.ReadBytes();
            if (!ModbusRtu.CrcOk(resp))
                throw new InvalidOperationException("M9811 Modbus response failed CRC.");
            if (resp.Length >= 3 && (resp[1] & 0x80) != 0)
                throw new InvalidOperationException($"M9811 Modbus exception 0x{resp[2]:X2}.");
            return resp;
        }

        /// <summary>Reads the model + firmware edition (MODEL/EDITION registers).</summary>
        public string Identify()
        {
            var r = Transact(ModbusRtu.ReadHoldingRegisters(SlaveAddress, RegModel, 2));
            ushort model = ModbusRtu.U16FromResponse(r, 3);
            ushort edition = ModbusRtu.U16FromResponse(r, 5);
            return $"Maynuo M9811 electronic load (model {model}, ver {edition})";
        }

        /// <summary>Places the load under remote control (PC1 coil = ON).</summary>
        public void Initialize() => Transact(ModbusRtu.ForceCoil(SlaveAddress, CoilRemote, true));

        /// <summary>Selects the regulation mode and setpoint: writes the setpoint register, then the CMD mode.</summary>
        public void SetMode(LoadMode mode, double setpoint)
        {
            ushort setReg, cmd;
            switch (mode)
            {
                case LoadMode.ConstantCurrent: setReg = RegCurrent; cmd = CmdCc; break;
                case LoadMode.ConstantVoltage: setReg = RegVoltage; cmd = CmdCv; break;
                case LoadMode.ConstantResistance: setReg = RegResistance; cmd = CmdCr; break;
                case LoadMode.ConstantPower: setReg = RegPower; cmd = CmdCw; break;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
            Transact(ModbusRtu.WriteFloat(SlaveAddress, setReg, (float)setpoint));
            Transact(ModbusRtu.WriteU16(SlaveAddress, RegCmd, cmd));
        }

        /// <summary>Enables the load input (CMD = Input ON).</summary>
        public void InputOn() => Transact(ModbusRtu.WriteU16(SlaveAddress, RegCmd, CmdInputOn));

        /// <summary>Disables the load input (CMD = Input OFF).</summary>
        public void InputOff() => Transact(ModbusRtu.WriteU16(SlaveAddress, RegCmd, CmdInputOff));

        /// <summary>Reads the measured terminal voltage (U register), in volts.</summary>
        public double ReadVoltage() =>
            ModbusRtu.FloatFromResponse(Transact(ModbusRtu.ReadHoldingRegisters(SlaveAddress, RegMeasVoltage, 2)), 3);

        /// <summary>Reads the measured sink current (I register), in amps.</summary>
        public double ReadCurrent() =>
            ModbusRtu.FloatFromResponse(Transact(ModbusRtu.ReadHoldingRegisters(SlaveAddress, RegMeasCurrent, 2)), 3);

        /// <summary>Reads sink power (W) — the load has no direct power register, so it is V×I.</summary>
        public double ReadPower() => ReadVoltage() * ReadCurrent();

        private static string ToHex(byte[] b)
        {
            var sb = new System.Text.StringBuilder(b.Length * 3);
            for (int i = 0; i < b.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("X2", CultureInfo.InvariantCulture)); }
            return sb.ToString();
        }
    }
}
