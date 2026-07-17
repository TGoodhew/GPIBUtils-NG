using System.Collections.Generic;
using GpibUtils.Visa.Srq;
using Xunit;

namespace GpibUtils.Visa.Tests.Srq
{
    /// <summary>
    /// Headless end-to-end tests for the completion state machine, driving the real
    /// <see cref="CompletionWaiter"/> against <see cref="SimulatedStatusChannel"/> with a virtual clock.
    /// The simulator and model mirror the behaviour confirmed on a real 8563E: the RQS mask and the
    /// read-back status byte share one layout, in which request-service is bit 0x40 (set on every SRQ,
    /// not an error), and command-complete is a condition that is true while idle - so the waiter must
    /// wait for the operation to go BUSY before accepting the next service request as the real
    /// completion. No hardware.
    /// </summary>
    public class CompletionWaiterTests
    {
        // The 8563E status model in the hardware-confirmed read-back layout (Table 7-266).
        private static StatusModel Model8563() => new StatusModel
        {
            SrqSupported = true,
            SerialPoll = new SerialPollSpec { ClearsRqs = true },
            EnableMask = new EnableMaskSpec { SetCommand = "RQS {mask}", ClearCommand = "RQS 0" },
            DoneSupport = new DoneSupportSpec { Supported = true, Mnemonic = "DONE" },
            ErrorBit = "error",
            RequestServiceBit = "requestService",
            Bits = new Dictionary<string, int>
            {
                ["message"] = 2, ["endOfSweep"] = 4, ["commandComplete"] = 16,
                ["error"] = 32, ["requestService"] = 64
            },
            Operations = new Dictionary<string, StatusOperation>
            {
                ["sweepComplete"] = new StatusOperation { Arm = "SNGLS;TS;", ExpectBit = "commandComplete", Restore = "CONTS;" },
                ["sweepAndPeak"] = new StatusOperation { Arm = "SNGLS;TS;MKPK HI;DONE;", ExpectBit = "commandComplete" }
            }
        };

        private static CompletionResult Run(StatusModel model, string op, int timeoutMs, SimulatedStatusChannel sim, int poll = 50) =>
            CompletionWaiter.Wait(model, "8563E", op, timeoutMs, sim, () => sim.Now, ms => sim.Advance(ms), poll);

        [Fact]
        public void SweepComplete_WaitsForRealCompletion_ViaRequestService()
        {
            // Idle at arm time (command-complete already true) - the stale case. The busy handshake must
            // wait for the FRESH sweep, not read the just-armed condition as a finished operation.
            var sim = new SimulatedStatusChannel { SweepDurationMs = 3000 };
            var result = Run(Model8563(), "sweepComplete", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(SimulatedStatusChannel.RequestService, result.StatusByte & SimulatedStatusChannel.RequestService);
            Assert.InRange(result.ElapsedMs, 3000, 3150);    // the full sweep, not ~0 (stale) or the backstop
            Assert.Equal(0, result.StatusByte & SimulatedStatusChannel.Error);

            Assert.Contains("RQS 0", sim.Sent);    // disarmed before arming
            Assert.Contains("RQS 48", sim.Sent);   // mask = commandComplete|error = 16|32 (NOT request-service)
            Assert.Contains("SNGLS;TS;", sim.Sent);
            Assert.Contains("CONTS;", sim.Sent);   // restored
        }

        [Fact]
        public void ErrorDuringSweep_ReportsInstrumentError()
        {
            // The sweep finishes (service requested) but an uncal/error is also set -> error bit at completion.
            var sim = new SimulatedStatusChannel { SweepDurationMs = 2000, ErrorOnSweep = true };
            var result = Run(Model8563(), "sweepComplete", 30000, sim);

            Assert.Equal(CompletionOutcome.InstrumentError, result.Outcome);
            Assert.Equal(SimulatedStatusChannel.Error, result.StatusByte & SimulatedStatusChannel.Error);
            Assert.InRange(result.ElapsedMs, 2000, 2150);    // still waited for the real completion
            Assert.Contains("signalled an ERROR", result.Message);
        }

        [Fact]
        public void NeverCompletes_TimesOutAtBackstop()
        {
            var sim = new SimulatedStatusChannel { SweepDurationMs = 100000 }; // longer than the timeout
            var result = Run(Model8563(), "sweepComplete", 3000, sim);

            Assert.Equal(CompletionOutcome.TimedOut, result.Outcome);
            Assert.InRange(result.ElapsedMs, 3000, 3150);
            Assert.Contains("RQS 0", sim.Sent);              // mask cleared, bus usable
        }

        [Fact]
        public void SweepAndPeak_CompletesViaRequestService()
        {
            var sim = new SimulatedStatusChannel { SweepDurationMs = 1500 };
            var result = Run(Model8563(), "sweepAndPeak", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(SimulatedStatusChannel.RequestService, result.StatusByte & SimulatedStatusChannel.RequestService);
            Assert.Contains("SNGLS;TS;MKPK HI;DONE;", sim.Sent);
        }

        // ---- legacy direct-bit flow (models without a requestServiceBit, e.g. the 3325) ----------

        [Fact]
        public void DirectBit_Model_PollsExpectBitDirectly()
        {
            var model = Model8563();
            model.RequestServiceBit = null;   // no request-service bit -> legacy direct-bit flow
            var sim = new SimulatedStatusChannel { SweepDurationMs = 2000 };
            var result = Run(model, "sweepComplete", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(SimulatedStatusChannel.CommandComplete, result.StatusByte & SimulatedStatusChannel.CommandComplete);
            Assert.InRange(result.ElapsedMs, 2000, 2150);
        }

        // ---- #96: pre-488.2 legacy status models --------------------------------

        [Fact]
        public void StatusReadViaQuery_ParsesMessyReply_AndCompletes()
        {
            // A pre-488.2 instrument whose status byte is read by an ASCII query ("STB?") rather than a
            // hardware serial poll. The reply is deliberately noisy (leading spaces, sign, exponent, CRLF)
            // to exercise the waiter's numeric parser. Completion is otherwise the normal SRQ-edge flow.
            var model = Model8563();
            model.StatusQuery = new StatusQuerySpec { Command = "STB?" };
            var sim = new SimulatedStatusChannel { SweepDurationMs = 2000 };
            sim.FormatStatusReply = v => string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "  +{0:0.0000E+00} \r\n", v);   // e.g. "  +8.4000E+01 \r\n"

            var result = Run(model, "sweepComplete", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(SimulatedStatusChannel.RequestService, result.StatusByte & SimulatedStatusChannel.RequestService);
            Assert.InRange(result.ElapsedMs, 2000, 2150);
        }

        [Fact]
        public void ExpectBitCleared_SettlesWhenOperatingBitDrops()
        {
            // A legacy source whose "operating" bit is asserted while busy and CLEARS when settled (the
            // HP 8672A pattern). Direct-bit flow (no requestServiceBit); completion is the bit going to 0.
            var model = new StatusModel
            {
                SrqSupported = true,
                SerialPoll = new SerialPollSpec { ClearsRqs = true },
                EnableMask = new EnableMaskSpec { SetCommand = "RQS {mask}", ClearCommand = "RQS 0" },
                ErrorBit = "error",
                Bits = new Dictionary<string, int> { ["operating"] = 8, ["error"] = 32 },
                Operations = new Dictionary<string, StatusOperation>
                {
                    ["settle"] = new StatusOperation { Arm = "TS;", ExpectBit = "operating", ExpectBitCleared = true }
                }
            };
            var sim = new SimulatedStatusChannel { SweepDurationMs = 2000 };
            var result = Run(model, "settle", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(0, result.StatusByte & SimulatedStatusChannel.Operating);   // settled (bit cleared)
            Assert.InRange(result.ElapsedMs, 2000, 2150);                            // waited for the real settle
        }

        [Fact]
        public void ExpectBitCleared_NeverSettles_TimesOut()
        {
            var model = new StatusModel
            {
                SrqSupported = true,
                EnableMask = new EnableMaskSpec { SetCommand = "RQS {mask}", ClearCommand = "RQS 0" },
                Bits = new Dictionary<string, int> { ["operating"] = 8 },
                Operations = new Dictionary<string, StatusOperation>
                {
                    ["settle"] = new StatusOperation { Arm = "TS;", ExpectBit = "operating", ExpectBitCleared = true }
                }
            };
            var sim = new SimulatedStatusChannel { SweepDurationMs = 100000 };   // never settles within timeout
            var result = Run(model, "settle", 3000, sim);

            Assert.Equal(CompletionOutcome.TimedOut, result.Outcome);
            Assert.Contains("not cleared", result.Message);
        }

        [Fact]
        public void ExpectBitCleared_NoEnableMask_Completes()
        {
            // A settle-on-clear source with NO *SRE-equivalent arm at all (e.g. the HP 8672A): completion is
            // pure polling of the operating bit going to 0, with no EnableMask to send. The waiter must not
            // demand one for a cleared-settle operation.
            var model = new StatusModel
            {
                SrqSupported = true,
                Bits = new Dictionary<string, int> { ["operating"] = 8 },
                Operations = new Dictionary<string, StatusOperation>
                {
                    ["settle"] = new StatusOperation { Arm = "TS;", ExpectBit = "operating", ExpectBitCleared = true }
                }
                // no EnableMask, no RequestServiceBit
            };
            var sim = new SimulatedStatusChannel { SweepDurationMs = 1500 };
            var result = Run(model, "settle", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(0, result.StatusByte & SimulatedStatusChannel.Operating);
        }

        [Fact]
        public void LegacyCustomVocabulary_DirectBit_Completes()
        {
            // Proves the engine hardcodes no bit names or commands: a fully custom pre-488.2 bit table and a
            // non-"RQS" enable-mask command (5005B-style "QM{mask}") complete through the direct-bit flow.
            var model = new StatusModel
            {
                SrqSupported = true,
                EnableMask = new EnableMaskSpec { SetCommand = "QM{mask}", ClearCommand = "QM0" },
                ErrorBit = "fault",
                Bits = new Dictionary<string, int> { ["ready"] = 16, ["fault"] = 32 },
                Operations = new Dictionary<string, StatusOperation>
                {
                    ["measure"] = new StatusOperation { Arm = "TS;", ExpectBit = "ready" }
                }
            };
            var sim = new SimulatedStatusChannel { SweepDurationMs = 1500 };
            var result = Run(model, "measure", 30000, sim);

            Assert.Equal(CompletionOutcome.Completed, result.Outcome);
            Assert.Equal(16, result.StatusByte & 16);
            Assert.Contains("QM48", sim.Sent);    // {mask}=ready|fault=16|32=48, substituted into the custom command
        }

        // ---- dispatch states (no I/O) -------------------------------------------

        [Fact]
        public void NoStatusModel_NeedsDefinition()
        {
            var sim = new SimulatedStatusChannel();
            var result = Run(null, "sweepComplete", 5000, sim);
            Assert.Equal(CompletionOutcome.NeedsDefinition, result.Outcome);
            Assert.Empty(sim.Sent); // nothing sent to the instrument
        }

        [Fact]
        public void SrqUnsupported_Refuses()
        {
            var sim = new SimulatedStatusChannel();
            var result = Run(new StatusModel { SrqSupported = false }, "sweepComplete", 5000, sim);
            Assert.Equal(CompletionOutcome.Refused, result.Outcome);
            Assert.Empty(sim.Sent);
        }

        [Fact]
        public void UnknownOperation_NeedsDefinition_ListsKnownOps()
        {
            var sim = new SimulatedStatusChannel();
            var result = Run(Model8563(), "bogus", 5000, sim);
            Assert.Equal(CompletionOutcome.NeedsDefinition, result.Outcome);
            Assert.Contains("sweepComplete", result.Message);
            Assert.Empty(sim.Sent);
        }
    }
}
