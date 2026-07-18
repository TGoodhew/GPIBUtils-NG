using System;

namespace GpibUtils.Instruments.ElectronicLoads
{
    /// <summary>
    /// Minimal Modbus RTU frame helper for the Maynuo loads: CRC-16 (poly 0xA001), the function codes the
    /// load uses — Read Holding Registers (0x03), Preset Multiple Registers (0x10), Force Single Coil (0x05),
    /// Read Coil Status (0x01) — and IEEE-754 float32 big-endian register packing (a setpoint occupies two
    /// consecutive registers, high word first, e.g. 2.3 A → registers 0x4013 0x3333 → bytes 40 13 33 33).
    /// </summary>
    internal static class ModbusRtu
    {
        /// <summary>Computes the Modbus CRC-16 over a byte range (returned with the low byte first when appended).</summary>
        public static ushort Crc16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];
                for (int b = 0; b < 8; b++)
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
            }
            return crc;
        }

        /// <summary>Appends the CRC (low byte, then high byte) to a request body and returns the full frame.</summary>
        public static byte[] Frame(byte[] body)
        {
            var crc = Crc16(body, 0, body.Length);
            var frame = new byte[body.Length + 2];
            Array.Copy(body, frame, body.Length);
            frame[body.Length] = (byte)(crc & 0xFF);
            frame[body.Length + 1] = (byte)(crc >> 8);
            return frame;
        }

        /// <summary>Validates a response frame's CRC and returns true when it checks out.</summary>
        public static bool CrcOk(byte[] frame)
        {
            if (frame == null || frame.Length < 4) return false;
            var crc = Crc16(frame, 0, frame.Length - 2);
            return frame[frame.Length - 2] == (byte)(crc & 0xFF) && frame[frame.Length - 1] == (byte)(crc >> 8);
        }

        /// <summary>Read Holding Registers (0x03): read <paramref name="count"/> registers from
        /// <paramref name="startAddress"/>.</summary>
        public static byte[] ReadHoldingRegisters(byte slave, ushort startAddress, ushort count) =>
            Frame(new[] { slave, (byte)0x03, (byte)(startAddress >> 8), (byte)startAddress, (byte)(count >> 8), (byte)count });

        /// <summary>Preset Multiple Registers (0x10) writing raw register words (high byte first per word).</summary>
        public static byte[] WriteRegisters(byte slave, ushort startAddress, ushort[] registers)
        {
            var body = new byte[7 + registers.Length * 2];
            body[0] = slave; body[1] = 0x10;
            body[2] = (byte)(startAddress >> 8); body[3] = (byte)startAddress;
            body[4] = (byte)(registers.Length >> 8); body[5] = (byte)registers.Length;
            body[6] = (byte)(registers.Length * 2);
            for (int i = 0; i < registers.Length; i++)
            {
                body[7 + i * 2] = (byte)(registers[i] >> 8);
                body[8 + i * 2] = (byte)registers[i];
            }
            return Frame(body);
        }

        /// <summary>Preset Multiple Registers (0x10) writing one float32 (two registers, high word first).</summary>
        public static byte[] WriteFloat(byte slave, ushort startAddress, float value) =>
            WriteRegisters(slave, startAddress, FloatToRegisters(value));

        /// <summary>Preset Multiple Registers (0x10) writing a single u16 register.</summary>
        public static byte[] WriteU16(byte slave, ushort address, ushort value) =>
            WriteRegisters(slave, address, new[] { value });

        /// <summary>Force Single Coil (0x05): ON = 0xFF00, OFF = 0x0000.</summary>
        public static byte[] ForceCoil(byte slave, ushort coilAddress, bool on) =>
            Frame(new[] { slave, (byte)0x05, (byte)(coilAddress >> 8), (byte)coilAddress, (byte)(on ? 0xFF : 0x00), (byte)0x00 });

        /// <summary>Packs a float32 into two registers, high word first (big-endian IEEE-754).</summary>
        public static ushort[] FloatToRegisters(float value)
        {
            var b = BitConverter.GetBytes(value);                 // little-endian on this platform
            if (BitConverter.IsLittleEndian) Array.Reverse(b);    // -> big-endian b0..b3
            return new[] { (ushort)((b[0] << 8) | b[1]), (ushort)((b[2] << 8) | b[3]) };
        }

        /// <summary>Reads a float32 from the 4 big-endian data bytes of a Read-Holding-Registers response,
        /// starting at <paramref name="dataOffset"/>.</summary>
        public static float FloatFromResponse(byte[] response, int dataOffset)
        {
            var le = new[] { response[dataOffset + 3], response[dataOffset + 2], response[dataOffset + 1], response[dataOffset + 0] };
            if (!BitConverter.IsLittleEndian) Array.Reverse(le);
            return BitConverter.ToSingle(le, 0);
        }

        /// <summary>Reads a u16 from a Read-Holding-Registers response at <paramref name="dataOffset"/>.</summary>
        public static ushort U16FromResponse(byte[] response, int dataOffset) =>
            (ushort)((response[dataOffset] << 8) | response[dataOffset + 1]);
    }
}
