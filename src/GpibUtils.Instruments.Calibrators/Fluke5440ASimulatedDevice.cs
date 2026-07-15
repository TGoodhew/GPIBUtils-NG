using System;
using System.Collections.Generic;
using System.Globalization;
using GpibUtils.Visa.Simulation;

namespace GpibUtils.Instruments.Calibrators
{
    /// <summary>
    /// An in-memory model of a Fluke 5440A/5440B for use with <see cref="SimulatedGpibProvider"/>, rich
    /// enough to drive the <see cref="Fluke5440A"/> driver end to end with no hardware. It decodes the
    /// mnemonic writes the driver sends (<c>SOUT</c>/<c>INCR</c>/<c>OPER</c>/<c>STBY</c>/<c>ESNS</c>/
    /// <c>ISNS</c>/limits/…) and answers the queries (<c>GVRS</c>/<c>GOUT</c>/<c>GERR</c>/<c>GONG</c>/
    /// <c>GSTS</c>/<c>GVLM</c>/<c>GCLM</c>/<c>GSRQ</c>/<c>GSPB</c>).
    ///
    /// <para>The 5440 has no <c>*IDN?</c>, so <see cref="SimulatedInstrument.IdentificationString"/> is
    /// unused; identity is modelled through the <c>GVRS</c> firmware response.</para>
    /// </summary>
    public sealed class Fluke5440ASimulatedDevice
    {
        /// <summary>The <see cref="SimulatedInstrument"/> to register with a <see cref="SimulatedGpibProvider"/>.</summary>
        public SimulatedInstrument Instrument { get; }

        private readonly List<string> _commands = new List<string>();

        /// <summary>Every command the calibrator was sent (writes and queries), in order (for assertions).</summary>
        public IReadOnlyList<string> Commands => _commands;

        /// <summary>Firmware version returned by <c>GVRS</c>.</summary>
        public string FirmwareVersion { get; set; } = "02.01";

        /// <summary>Present programmed output level (V), returned by <c>GOUT</c>.</summary>
        public double OutputVolts { get; private set; }

        /// <summary>Whether the output is in Operate (true) or Standby (false).</summary>
        public bool IsOperating { get; private set; }

        /// <summary>True = external 4-wire sense (<c>ESNS</c>); false = internal 2-wire (<c>ISNS</c>); null unset.</summary>
        public bool? ExternalSense { get; private set; }

        /// <summary>Stored reference level (from <c>SREF</c>).</summary>
        public double Reference { get; private set; }

        /// <summary>Voltage limit set by <c>SVLM</c>.</summary>
        public double VoltageLimit { get; private set; } = 1100.0;

        /// <summary>Current limit set by <c>SCLM</c>.</summary>
        public double CurrentLimit { get; private set; } = 0.020;

        /// <summary>SRQ mask set by <c>SSRQ</c>.</summary>
        public int SrqMask { get; private set; }

        /// <summary>The error code the next <c>GERR</c> returns (and clears). Default 0 = no error.</summary>
        public int PendingError { get; set; }

        /// <summary>The "doing" state the next <c>GONG</c> returns. Default 0 = idle.</summary>
        public int DoingState { get; set; }

        public Fluke5440ASimulatedDevice()
        {
            Instrument = new SimulatedInstrument
            {
                IdentificationString = "FLUKE,5440B,0," /* unused: 5440 has no *IDN? */,
                WriteObserver = Apply,
                Responder = Respond
            };
        }

        private void Apply(string command)
        {
            var cmd = command.Trim();
            if (cmd.Length == 0) return;
            _commands.Add(cmd);

            var upper = cmd.ToUpperInvariant();
            var (mnemonic, arg) = Split(upper);

            switch (mnemonic)
            {
                case "RESET":
                    OutputVolts = 0; IsOperating = false; Instrument.StatusByte = 0; break;
                case "SOUT": if (TryVal(arg, out var v)) OutputVolts = v; break;
                case "INCR": if (TryVal(arg, out var d)) OutputVolts += d; break;
                case "SREF": Reference = OutputVolts; break;
                case "GREF": OutputVolts = Reference; break;
                case "OPER": IsOperating = true; break;
                case "STBY": IsOperating = false; break;
                case "ESNS": ExternalSense = true; break;
                case "ISNS": ExternalSense = false; break;
                case "SVLM": if (TryVal(arg, out var vl)) VoltageLimit = vl; break;
                case "SCLM": if (TryVal(arg, out var cl)) CurrentLimit = cl; break;
                case "SSRQ": if (int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)) SrqMask = m; break;
                // BSTV/BSTC/BSTO, EGRD/IGRD, DIVY/DIVN, TSTA/TSTD/TSTH, cal — accepted, no modelled state.
            }
        }

        private string Respond(string command)
        {
            var upper = (command ?? string.Empty).Trim().ToUpperInvariant();
            var (mnemonic, _) = Split(upper);
            switch (mnemonic)
            {
                case "GVRS": return FirmwareVersion;
                case "GOUT": return Fmt(OutputVolts);
                case "GVLM": return Fmt(VoltageLimit);
                case "GCLM": return Fmt(CurrentLimit);
                case "GSRQ": return SrqMask.ToString(CultureInfo.InvariantCulture);
                case "GSPB": return ((int)Instrument.StatusByte).ToString(CultureInfo.InvariantCulture);
                case "GSTS": return "0";
                case "GONG": return DoingState.ToString(CultureInfo.InvariantCulture);
                case "GERR":
                    var e = PendingError; PendingError = 0; return e.ToString(CultureInfo.InvariantCulture);
                default:
                    return null;   // not a query we model
            }
        }

        private static (string mnemonic, string arg) Split(string upper)
        {
            int sp = upper.IndexOf(' ');
            return sp < 0 ? (upper, string.Empty) : (upper.Substring(0, sp), upper.Substring(sp + 1).Trim());
        }

        private static bool TryVal(string s, out double value) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string Fmt(double v) => v.ToString("G7", CultureInfo.InvariantCulture);
    }
}
