using System;

namespace GpibUtils.Visa
{
    /// <summary>
    /// An IEEE 488.1 serial-poll status byte with the standard 488.2 bit meanings decoded.
    /// </summary>
    public readonly struct StatusByte : IEquatable<StatusByte>
    {
        /// <summary>The raw status byte (0-255).</summary>
        public byte Value { get; }

        public StatusByte(byte value) { Value = value; }

        /// <summary>Bit 6 (0x40) — RQS/MSS: the device is requesting service.</summary>
        public bool RequestingService => (Value & 0x40) != 0;

        /// <summary>Bit 5 (0x20) — ESB: an enabled event in the standard event status register.</summary>
        public bool EventStatus => (Value & 0x20) != 0;

        /// <summary>Bit 4 (0x10) — MAV: a message is available in the output queue.</summary>
        public bool MessageAvailable => (Value & 0x10) != 0;

        /// <summary>Reads an individual bit (0-7).</summary>
        public bool this[int bit]
        {
            get
            {
                if (bit < 0 || bit > 7) throw new ArgumentOutOfRangeException(nameof(bit));
                return (Value & (1 << bit)) != 0;
            }
        }

        public bool Equals(StatusByte other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StatusByte other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => "0x" + Value.ToString("X2");
    }
}
