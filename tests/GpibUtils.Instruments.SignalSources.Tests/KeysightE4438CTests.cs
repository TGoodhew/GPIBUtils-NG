using System;
using System.Linq;
using GpibUtils.Instruments.SignalSources;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.SignalSources.Tests
{
    /// <summary>Drives the <see cref="KeysightE4438C"/> driver against a simulated ESG over the standard transport.</summary>
    public class KeysightE4438CTests
    {
        private static (KeysightE4438C driver, KeysightE4438CSimulatedDevice sim, IInstrumentSession session) Bench()
        {
            var provider = new SimulatedGpibProvider();
            var sim = new KeysightE4438CSimulatedDevice();
            provider.Add(KeysightE4438C.DefaultResource, sim.Instrument);
            var session = provider.Open(KeysightE4438C.DefaultResource);
            return (new KeysightE4438C(session), sim, session);
        }

        [Fact]
        public void Is_a_signal_source()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<ISignalSource>(driver);
        }

        [Fact]
        public void Identify_returns_idn()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Contains("E4438C", driver.Identify());
        }

        [Fact]
        public void Initialize_clears_resets_and_rf_off()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("*RST", driver.History);
                Assert.Contains("*CLS", driver.History);
                Assert.False(sim.RfOn);
            }
        }

        [Fact]
        public void Frequency_hz_round_trips()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyHz(2.4e9);
                Assert.Equal(2.4e9, sim.FrequencyHz, 0);
                Assert.Equal(2.4e9, driver.GetFrequencyHz(), 0);
                Assert.Contains(":FREQuency:FIXed 2400000000 Hz", driver.History);
            }
        }

        [Fact]
        public void Frequency_mhz_maps_to_hz()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetFrequencyMHz(1000);   // 1 GHz
                Assert.Equal(1e9, sim.FrequencyHz, 0);
            }
        }

        [Fact]
        public void Min_max_frequency_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MinFrequencyHz = 250e3;
                sim.MaxFrequencyHz = 6e9;
                Assert.Equal(250e3, driver.GetMinFrequencyHz(), 0);
                Assert.Equal(6e9, driver.GetMaxFrequencyHz(), 0);
            }
        }

        [Fact]
        public void Power_round_trips()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetPowerDbm(-5.5);
                Assert.Equal(-5.5, sim.PowerDbm, 3);
                Assert.Equal(-5.5, driver.GetPowerDbm(), 3);
                Assert.Contains(":POWer:LEVel -5.5 dBm", driver.History);
            }
        }

        [Fact]
        public void Min_max_power_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.MinPowerDbm = -136;
                sim.MaxPowerDbm = 20;
                Assert.Equal(-136, driver.GetMinPowerDbm(), 0);
                Assert.Equal(20, driver.GetMaxPowerDbm(), 0);
            }
        }

        [Fact]
        public void Rf_and_modulation_toggle()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.RfOn(); Assert.True(sim.RfOn);
                driver.RfOff(); Assert.False(sim.RfOn);
                driver.SetModulation(true); Assert.True(sim.ModulationOn);
                driver.SetModulation(false); Assert.False(sim.ModulationOn);
            }
        }

        [Fact]
        public void Reference_auto_and_source()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SetReferenceAuto(true);
                Assert.True(sim.ReferenceAuto);
                sim.ReferenceSource = "EXT";
                Assert.Equal("EXT", driver.GetReferenceSource());
            }
        }

        [Fact]
        public void Interleave_is_big_endian_two_complement()
        {
            // I = 0x0102 (258), Q = -2 (0xFFFE): bytes I_hi,I_lo,Q_hi,Q_lo
            var bytes = KeysightE4438C.InterleaveBigEndian(new short[] { 0x0102 }, new short[] { -2 });
            Assert.Equal(new byte[] { 0x01, 0x02, 0xFF, 0xFE }, bytes);
        }

        [Fact]
        public void Ieee4882_block_frames_definite_length()
        {
            var msg = KeysightE4438C.Ieee4882Block("HDR,", new byte[] { 1, 2, 3, 4 });
            // "HDR,#14" + 4 payload bytes
            Assert.Equal((byte)'#', msg[4]);
            Assert.Equal((byte)'1', msg[5]);   // one digit for the length
            Assert.Equal((byte)'4', msg[6]);   // length = 4
            Assert.Equal(11, msg.Length);
        }

        [Fact]
        public void Download_waveform_records_segment_and_completes()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.DownloadWaveform("seg1", new short[] { 1, 2, 3 }, new short[] { 4, 5, 6 });
                Assert.Contains("seg1", sim.VolatileSegments);
                // ARB turned off before download; *OPC? + error read-back issued.
                Assert.Contains(":RADio:ARB:STATe OFF", driver.History);
                Assert.Contains("*OPC?", driver.History);
                Assert.Contains(":SYSTem:ERRor?", driver.History);
            }
        }

        [Fact]
        public void Download_waveform_throws_on_instrument_error()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = "-223,\"Too much data\"";
                Assert.Throws<InvalidOperationException>(() =>
                    driver.DownloadWaveform("seg1", new short[] { 1 }, new short[] { 2 }));
            }
        }

        [Fact]
        public void Download_waveform_rejects_mismatched_lengths()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentException>(() =>
                    driver.DownloadWaveform("seg1", new short[] { 1, 2 }, new short[] { 3 }));
        }

        [Fact]
        public void Segment_name_too_long_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentException>(() =>
                    driver.DownloadWaveform(new string('x', 24), new short[] { 1 }, new short[] { 2 }));
        }

        [Fact]
        public void Play_waveform_selects_sets_clock_and_arms_arb()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.DownloadWaveform("wave", new short[] { 1 }, new short[] { 2 });
                driver.PlayWaveform("wave", 50e6);
                Assert.Equal("wave", sim.SelectedWaveform);
                Assert.True(sim.ArbOn);
                Assert.Contains(":RADio:ARB:SCLock:RATE 50000000", driver.History);
            }
        }

        [Fact]
        public void Copy_to_and_from_non_volatile()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.DownloadWaveform("keep", new short[] { 1 }, new short[] { 2 });
                driver.CopyToNonVolatile("keep");
                Assert.Contains("keep", sim.NonVolatileSegments);
                driver.LoadFromNonVolatile("keep");
                Assert.Contains("keep", sim.VolatileSegments);
            }
        }

        [Fact]
        public void Error_query_reads_and_clears()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.PendingError = "-113,\"Undefined header\"";
                Assert.Contains("Undefined header", driver.GetError());
                Assert.Contains("No error", driver.GetError());
            }
        }

        [Fact]
        public void No_error_recognises_zero_codes()
        {
            Assert.True(KeysightE4438C.IsNoError("+0,\"No error\""));
            Assert.True(KeysightE4438C.IsNoError("0,\"No error\""));
            Assert.True(KeysightE4438C.IsNoError(""));
            Assert.False(KeysightE4438C.IsNoError("-222,\"Data out of range\""));
        }
    }
}
