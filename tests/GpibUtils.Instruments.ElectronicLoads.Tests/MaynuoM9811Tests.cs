using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.ElectronicLoads.Tests
{
    public class MaynuoM9811Tests
    {
        private static (MaynuoM9811 load, MaynuoM9811SimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new MaynuoM9811SimulatedDevice();
            provider.Add(MaynuoM9811.DefaultResource, sim.Instrument);
            var session = provider.Open(MaynuoM9811.DefaultResource);
            return (new MaynuoM9811(session), sim, session);
        }

        // ---- Modbus helper against the manual's own worked frames ----

        [Fact]
        public void Crc16_matches_the_manual_examples()
        {
            // Read ISTATE coil: body 01 01 05 10 00 01 -> CRC FC C3 (low, high).
            Assert.Equal(new byte[] { 0x01, 0x01, 0x05, 0x10, 0x00, 0x01, 0xFC, 0xC3 },
                ModbusRtu.Frame(new byte[] { 0x01, 0x01, 0x05, 0x10, 0x00, 0x01 }));
            // Force PC1 ON: 01 05 05 00 FF 00 -> CRC 8C F6.
            Assert.Equal(new byte[] { 0x01, 0x05, 0x05, 0x00, 0xFF, 0x00, 0x8C, 0xF6 },
                ModbusRtu.ForceCoil(0x01, 0x0500, true));
        }

        [Fact]
        public void Write_ifix_2_3A_matches_the_manual_frame()
        {
            // Manual: set IFIX 2.3 A -> 01 10 0A 01 00 02 04 40 13 33 33 FC 23.
            Assert.Equal(new byte[] { 0x01, 0x10, 0x0A, 0x01, 0x00, 0x02, 0x04, 0x40, 0x13, 0x33, 0x33, 0xFC, 0x23 },
                ModbusRtu.WriteFloat(0x01, 0x0A01, 2.3f));
        }

        [Fact]
        public void Float_packs_big_endian_high_word_first()
        {
            Assert.Equal(new ushort[] { 0x4013, 0x3333 }, ModbusRtu.FloatToRegisters(2.3f));
        }

        // ---- driver against the simulator ----

        [Fact]
        public void Implements_electronic_load_and_default_resource_is_serial()
        {
            var (d, _, s) = Bench();
            using (s)
            {
                Assert.IsAssignableFrom<IElectronicLoad>(d);
                Assert.Equal("ASRL1::INSTR", MaynuoM9811.DefaultResource);
                Assert.Contains("9811", d.Identify());   // MODEL register
            }
        }

        [Fact]
        public void Initialize_forces_remote_control_coil()
        {
            var (d, sim, s) = Bench();
            using (s)
            {
                d.Initialize();
                Assert.True(sim.Coils[0x0500]);   // PC1 = remote
            }
        }

        [Fact]
        public void Set_cc_writes_setpoint_then_mode()
        {
            var (d, sim, s) = Bench();
            using (s)
            {
                d.SetMode(LoadMode.ConstantCurrent, 2.3);
                Assert.Equal(2.3f, sim.SetpointAt(0x0A01), 3);   // IFIX
                Assert.Equal(1, sim.LastCommand);                 // CMD = CC
            }
        }

        [Fact]
        public void Set_cv_and_cr_use_the_right_registers_and_modes()
        {
            var (d, sim, s) = Bench();
            using (s)
            {
                d.SetMode(LoadMode.ConstantVoltage, 12.0);
                Assert.Equal(12.0f, sim.SetpointAt(0x0A03), 3);   // UFIX
                Assert.Equal(2, sim.LastCommand);

                d.SetMode(LoadMode.ConstantResistance, 50.0);
                Assert.Equal(50.0f, sim.SetpointAt(0x0A07), 3);   // RFIX
                Assert.Equal(4, sim.LastCommand);
            }
        }

        [Fact]
        public void Input_on_off_write_the_cmd_register()
        {
            var (d, sim, s) = Bench();
            using (s)
            {
                d.InputOn(); Assert.Equal(42, sim.LastCommand);
                d.InputOff(); Assert.Equal(43, sim.LastCommand);
            }
        }

        [Fact]
        public void Reads_measured_voltage_current_and_power()
        {
            var (d, sim, s) = Bench();
            using (s)
            {
                sim.MeasuredVoltage = 12.0f;
                sim.MeasuredCurrent = 1.5f;
                Assert.Equal(12.0, d.ReadVoltage(), 3);
                Assert.Equal(1.5, d.ReadCurrent(), 3);
                Assert.Equal(18.0, d.ReadPower(), 3);   // V x I
            }
        }
    }
}
