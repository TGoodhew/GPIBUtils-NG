using System.Collections.Generic;
using System.Text;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.ElectronicLoads
{
    /// <summary>
    /// An in-memory Modbus-RTU slave model of the Maynuo M9811 for use with <see cref="SimulatedGpibProvider"/>,
    /// rich enough to drive the <see cref="MaynuoM9811"/> driver end to end with no hardware. It answers Read
    /// Holding Registers (0x03), Preset Multiple Registers (0x10) and Force Single Coil (0x05), holding the
    /// written setpoints/CMD and exposing settable measured voltage/current.
    ///
    /// <para>Note: the shared simulated session trims a trailing CR/LF from a write, so request frames whose
    /// final CRC byte is 0x0D/0x0A would be corrupted in simulation only — pick test values that avoid it (real
    /// serial transports do not trim).</para>
    /// </summary>
    public sealed class MaynuoM9811SimulatedDevice
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        public SimulatedInstrument Instrument { get; }

        /// <summary>Holding registers written by the driver (address → word).</summary>
        public Dictionary<ushort, ushort> Registers { get; } = new Dictionary<ushort, ushort>();

        /// <summary>Coils forced by the driver (address → state).</summary>
        public Dictionary<ushort, bool> Coils { get; } = new Dictionary<ushort, bool>();

        /// <summary>Last value written to the CMD register (0x0A00) — mode / input on-off.</summary>
        public ushort LastCommand { get; private set; }

        /// <summary>Measured terminal voltage the load reports (register U, 0x0B00).</summary>
        public float MeasuredVoltage { get; set; } = 0f;

        /// <summary>Measured sink current the load reports (register I, 0x0B02).</summary>
        public float MeasuredCurrent { get; set; } = 0f;

        public ushort Model { get; set; } = 9811;
        public ushort Edition { get; set; } = 100;

        public MaynuoM9811SimulatedDevice()
        {
            Instrument = new SimulatedInstrument { IdentificationString = "MAYNUO,M9811", Responder = Respond };
        }

        /// <summary>Reads back a float32 setpoint the driver wrote (two registers, high word first).</summary>
        public float SetpointAt(ushort address)
        {
            ushort hi = Registers.TryGetValue(address, out var h) ? h : (ushort)0;
            ushort lo = Registers.TryGetValue((ushort)(address + 1), out var l) ? l : (ushort)0;
            var le = new[] { (byte)lo, (byte)(lo >> 8), (byte)hi, (byte)(hi >> 8) };
            if (!System.BitConverter.IsLittleEndian) System.Array.Reverse(le);
            return System.BitConverter.ToSingle(le, 0);
        }

        private string Respond(string request)
        {
            var b = Latin1.GetBytes(request ?? string.Empty);
            if (!ModbusRtu.CrcOk(b)) return null;
            byte slave = b[0], func = b[1];

            switch (func)
            {
                case 0x03:   // Read Holding Registers
                {
                    ushort start = (ushort)((b[2] << 8) | b[3]);
                    ushort count = (ushort)((b[4] << 8) | b[5]);
                    var data = new byte[count * 2];
                    for (int i = 0; i < count; i++)
                    {
                        ushort word = WordAt((ushort)(start + i));
                        data[i * 2] = (byte)(word >> 8);
                        data[i * 2 + 1] = (byte)word;
                    }
                    var body = new byte[3 + data.Length];
                    body[0] = slave; body[1] = 0x03; body[2] = (byte)data.Length;
                    System.Array.Copy(data, 0, body, 3, data.Length);
                    return Latin1.GetString(ModbusRtu.Frame(body));
                }
                case 0x10:   // Preset Multiple Registers
                {
                    ushort start = (ushort)((b[2] << 8) | b[3]);
                    ushort count = (ushort)((b[4] << 8) | b[5]);
                    for (int i = 0; i < count; i++)
                        Registers[(ushort)(start + i)] = (ushort)((b[7 + i * 2] << 8) | b[8 + i * 2]);
                    if (start == 0x0A00) LastCommand = Registers[0x0A00];
                    var body = new[] { slave, (byte)0x10, b[2], b[3], b[4], b[5] };
                    return Latin1.GetString(ModbusRtu.Frame(body));
                }
                case 0x05:   // Force Single Coil
                {
                    ushort addr = (ushort)((b[2] << 8) | b[3]);
                    Coils[addr] = b[4] == 0xFF;
                    return request;   // a normal response echoes the request frame
                }
                default:
                    return null;
            }
        }

        private ushort WordAt(ushort address)
        {
            var v = ModbusRtu.FloatToRegisters(MeasuredVoltage);
            var i = ModbusRtu.FloatToRegisters(MeasuredCurrent);
            switch (address)
            {
                case 0x0B00: return v[0];
                case 0x0B01: return v[1];
                case 0x0B02: return i[0];
                case 0x0B03: return i[1];
                case 0x0B06: return Model;
                case 0x0B07: return Edition;
                default: return Registers.TryGetValue(address, out var w) ? w : (ushort)0;
            }
        }
    }
}
