# Changelog

All notable changes to **GPIBUtils-NG** are recorded here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project will adopt
[Semantic Versioning](https://semver.org/) once a first release is cut.

> **No-hardware build policy.** Development currently happens without access to the physical
> instruments. Code lands on `main` once it builds and passes the **Simulated**-provider unit tests;
> real-hardware confirmation is tracked separately and is **not** a merge gate. Entries below that
> touch an instrument driver are marked with their verification state:
> 🟡 **Verification Needed** (merged, awaiting bench) · ✅ **HW-Verified** (confirmed on real hardware).
> The live per-instrument status lives in [`docs/HARDWARE_VERIFICATION.md`](docs/HARDWARE_VERIFICATION.md).

## [Unreleased]

### Added

- **Keithley 2015 audio-distortion surface** via a **new `IAudioDistortionAnalyzer` interface** (issue
  [#94](https://github.com/TGoodhew/GPIBUtils-NG/issues/94)) — the 2015 now exposes THD / THD+N / SINAD
  (`:SENS:FUNC 'DIST'`, `:SENS:DIST:TYPE`, `:SENS:DIST:FREQ`, low/high cutoffs) alongside its existing
  `IDigitalMultimeter` DMM surface; the 2015P `:DIST:PEAK:*` spectrum peak-search is exposed as extra concrete
  methods. CLI `keithley2015 distortion thd|thdn|sinad`. Tests (+4). 🟡 **Verification Needed.**

- **HP 3245A Universal Source** (issue [#105](https://github.com/TGoodhew/GPIBUtils-NG/issues/105)) —
  a driver in `GpibUtils.Instruments.SignalSources` implementing a **new `IUniversalSource` interface** (P1 #89):
  a multi-channel precision DC voltage/current source + low-frequency waveform generator, a shape that
  `ISignalSource` / `IDcVoltageCalibrator` / `IDcPowerSupply` cannot express. Keyword language (`APPLY DCV`/
  `APPLY DCI`, `USE 0`/`USE 100`, `ARANGE`, `OUTPUT?`, `RST`, `ID?` — no `*`-prefixed 488.2 command), ±10.25 V /
  ±0.1 A range enforcement, `RQS` mask exposed for the shared Srq engine (custom 6-bit status register). CLI
  `hp3245a idn|dc`, default address **9** (factory-confirmed). Tests (+6). **Needs bench verification.**

- **HP 8663A Synthesized Signal Generator** (issue [#124](https://github.com/TGoodhew/GPIBUtils-NG/issues/124)) —
  a driver in `GpibUtils.Instruments.SignalSources` implementing `ISignalSource` (legacy two-letter mnemonics
  `FR…MZ` / `AP…DM`, Device-Clear preset). The 8663A has no dedicated RF on/off key, so `RfOff()` mutes to the
  spec floor and `RfOn()` restores the last commanded amplitude; the RQS mask (`@1`) is exposed for the shared
  Srq engine. CLI `hp8663a apply`. Tests (+5). **Needs bench verification** (no documented GPIB address —
  placeholder 19; RF-off mapping).
- **HP 3335A Synthesizer/Level Generator** (issue [#107](https://github.com/TGoodhew/GPIBUtils-NG/issues/107)) —
  a standalone driver in `GpibUtils.Instruments.SignalSources` (single-character key codes `F…`/`A…`, the unit
  key doubling as the entry terminator and amplitude-sign selector: `K`=+dBm, `M`=−dBm). Deliberately **not**
  `ISignalSource` — the instrument is listen-only (no readback, no `*IDN?`) and has no remote RF on/off, the
  same decision as the HP 8350B. CLI `hp3335a idn|set`. Tests (+3). **Needs bench verification** (address).
- **HP 5343A Microwave Frequency Counter** (issue [#114](https://github.com/TGoodhew/GPIBUtils-NG/issues/114)) —
  a standalone driver in `GpibUtils.Instruments.Counters` (26.5 GHz sibling of the 5342A): `AU`/`M` acquisition,
  `L`/`H` range, `SR{n}` resolution, `SM…E` manual center frequency, `ST{n}` output mode, and a fixed-width
  `F NNNNN.NNNNNN E 06` reading parsed to Hz with dashes/all-9s/all-0s error indications surfaced as exceptions.
  Follows the 5342A/5351A precedent (not `IFrequencyCounter`). CLI `hp5343a idn|init|freq`. Tests (+9).
  **Needs bench verification** (OCR-ambiguous one-letter `M`/`R` codes; example address 2).

- **HP 8970B Noise Figure Meter** (issue [#132](https://github.com/TGoodhew/GPIBUtils-NG/issues/132)) —
  a driver in a **new `GpibUtils.Instruments.NoiseFigureMeters` project** implementing a **new
  `INoiseFigureMeter` interface** (P1 #85): sets a fixed or start/stop frequency, selects uncorrected NF
  (`M1`) or corrected NF+Gain (`M2`), triggers (`T2`) and reads back a dual noise-figure/gain result.
  Legacy concatenated mnemonics (`FA<f>EN`/`FB<f>EN`/`FR<f>EN`, `M1`/`M2`, `T1`/`T2`, `RS`), 12-byte
  exponential ASCII record (`sDDDDDEsNN`), `≥9×10^10` error sentinel. CLI `hp8970b idn|measure`. Tests
  (+6). **Needs bench verification** (address, read format and the ΦM/gain mnemonics are a manual-based
  reconstruction).

- **HP 8901A/8901B Modulation Analyzer** (issue [#130](https://github.com/TGoodhew/GPIBUtils-NG/issues/130)) —
  a driver in `GpibUtils.Instruments.Meters` implementing a **new `IModulationAnalyzer` interface** (P1 #91):
  tunes to a carrier and measures AM/FM/ΦM/RF-power/frequency. Legacy concatenated function codes (`IP`,
  `AU <f> MZ`, `M1`=AM/`M2`=FM, `T3` trigger-with-settling), 17-char exponential ASCII result. Tests (+5);
  `gpibutils hp8901 idn|measure am|fm|… -f <MHz>`. M3–M5 (ΦM/power/freq) codes are bench-confirm. 🟡
  **Verification Needed.**
- **`IModulationDomainAnalyzer` + new `GpibUtils.Instruments.ModulationDomain` project** (P1 #87) with the
  **HP 53310A** ([#113](https://github.com/TGoodhew/GPIBUtils-NG/issues/113), SCPI, addr 12): frequency-vs-time
  / time-interval-vs-time (and histogram) measurements via the `:CONFigure` + blocking `:READ?` flow — a data
  model that fits neither a spectrum analyzer, counter, nor scope. Tests (+5); `gpibutils hp53310a
  idn|measure freq|tinterval|…`. (The manual's `:STATus:OPERation` bit-4 SRQ-edge completion maps onto the
  shared Srq engine — a follow-up if async completion is needed.) 🟡 **Verification Needed.**
- **HP 8508A Vector Voltmeter** (issue [#104](https://github.com/TGoodhew/GPIBUtils-NG/issues/104),
  **re-scoped** from the mislabeled "Fluke 8508A" — the physical instrument is the HP vector voltmeter, a
  model-number collision) — a driver in `GpibUtils.Instruments.Meters` implementing a **new `IVectorVoltmeter`
  interface**: a tuned dual-channel RF receiver (100 kHz–2 GHz) measuring channel A/B voltage/power, B/A
  ratio, B−A phase, transmission, group delay, SWR, reflection coefficient, admittance, impedance. IEEE-488.2
  mnemonic (`SENSe`/`MEASure?`/`FREQuency:BAND:AUTO`/`AVERage:COUNt`). Built from the 8508A User Guide command
  set. Tests (+5); `gpibutils hp8508a idn|measure <quantity>`. 🟡 **Verification Needed.**
- **`ISourceMeasureUnit` + new `GpibUtils.Instruments.SourceMeasure` project** (P1 #84) with the **Keithley
  2400 SourceMeter** ([#134](https://github.com/TGoodhew/GPIBUtils-NG/issues/134), SCPI, addr 24). Sources
  voltage or current with a compliance limit (current limit when sourcing V, voltage limit when sourcing I)
  and reads back V/I/R with a single blocking `:READ?` (`:FORMat:ELEMents VOLTage,CURRent,RESistance`). Tests
  (+4); `gpibutils keithley2400 idn|measure`. 🟡 **Verification Needed.**
- **`INetworkAnalyzer` + new `GpibUtils.Instruments.NetworkAnalyzers` project** (P1 #82) with two implementers
  from the #70 triage: **HP 8711C-8714C** ([#127](https://github.com/TGoodhew/GPIBUtils-NG/issues/127), native
  SCPI, addr 16) and **HP 8720C** microwave VNA ([#128](https://github.com/TGoodhew/GPIBUtils-NG/issues/128),
  legacy front-panel mnemonics + IEEE-488.2, addr 16). Scalar-first, vector-extensible interface: frequency
  sweep, source power, S-parameter select, single-sweep blocking on `*OPC?` (mandatory before any data read),
  formatted-trace read, peak marker. Tests (+3); generic `gpibutils hp8714|hp8720c sweep` CLI. The scalar
  **HP 8757D (#129) is deferred** — its command syntax lives only in an unavailable Quick Reference Guide, so
  building it would mean inventing mnemonics. 🟡 **Verification Needed.**

- **Analyzers / meters / supply batch (6)** (from the #70 triage): **Rigol DSA800**
  ([#102](https://github.com/TGoodhew/GPIBUtils-NG/issues/102)) + **Agilent N9320A**
  ([#136](https://github.com/TGoodhew/GPIBUtils-NG/issues/136)) SCPI spectrum analyzers (shared
  `ScpiSpectrumAnalyzer` base, `*OPC?`-blocking single sweep, `ISpectrumAnalyzer`; bench-confirm — no readable
  programming guides); **Keithley 2015** ([#133](https://github.com/TGoodhew/GPIBUtils-NG/issues/133)) THD DMM
  (full `IDigitalMultimeter`, Keithley 2000-family SCPI); **HP 437B**
  ([#111](https://github.com/TGoodhew/GPIBUtils-NG/issues/111)) + **HP 436A**
  ([#110](https://github.com/TGoodhew/GPIBUtils-NG/issues/110)) RF power meters (`IPowerMeter`; 437B has
  `*IDN?`, 436A parses the fixed 14-char output string); **HP 6625A**
  ([#117](https://github.com/TGoodhew/GPIBUtils-NG/issues/117)) dual-output DC supply (`IDcPowerSupply`,
  channel-scoped `VSET`/`ISET`/`VOUT?`). Batch tests (+8); generic `sweep`/`measure` CLIs + `keithley2015
  measure` / `hp6625a set`. 🟡 **Verification Needed.**
- **Seven oscilloscopes** (from the #70 triage) on the existing `IOscilloscope` in
  `GpibUtils.Instruments.Scopes`, across three command dialects (one shared base each): **Tektronix**
  DPO3000 ([#100](https://github.com/TGoodhew/GPIBUtils-NG/issues/100)), DPO4000
  ([#101](https://github.com/TGoodhew/GPIBUtils-NG/issues/101)), TDS784
  ([#139](https://github.com/TGoodhew/GPIBUtils-NG/issues/139)) — Tek SCPI (`ACQuire`/`SELect`/`AUTOSet`/
  `MEASUrement:IMMed`); **Agilent** 54622A ([#115](https://github.com/TGoodhew/GPIBUtils-NG/issues/115)),
  54845A Infiniium ([#116](https://github.com/TGoodhew/GPIBUtils-NG/issues/116)) — root-level `:RUN`/`:STOP`/
  `:SINGle`/`:AUToscale`, `:MEASure:VPP?`; **LeCroy** LC574A
  ([#135](https://github.com/TGoodhew/GPIBUtils-NG/issues/135)), WaveRunner 6000
  ([#140](https://github.com/TGoodhew/GPIBUtils-NG/issues/140)) — `Cn:`-prefixed dialect (`TRMD`/`Cn:TRA`/
  `ASET`/`Cn:PAVA?`, bench-confirm — the LeCroy remote manuals were unreadable). Batch tests (+8); a generic
  `gpibutils <scope> ctl --acq run|stop|single|auto --vpp -c <n>` CLI shared over all IOscilloscope drivers.
  🟡 **Verification Needed.**
- **Eight ISignalSource RF generators** (from the #70 triage) in `GpibUtils.Instruments.SignalSources`, all
  CW frequency/power/RF-on-off: **Agilent E4436B** ([#103](https://github.com/TGoodhew/GPIBUtils-NG/issues/103),
  SCPI, addr 19), **HP 83620A** ([#119](https://github.com/TGoodhew/GPIBUtils-NG/issues/119), SCPI, addr 19),
  **HP 83712B** ([#120](https://github.com/TGoodhew/GPIBUtils-NG/issues/120), SCPI, addr 19),
  **HP 8656A/B** ([#122](https://github.com/TGoodhew/GPIBUtils-NG/issues/122), legacy mnemonic, write-only,
  addr 7), **HP 8657B** ([#123](https://github.com/TGoodhew/GPIBUtils-NG/issues/123), legacy mnemonic,
  listen-only, addr 7), **HP 8664A** ([#125](https://github.com/TGoodhew/GPIBUtils-NG/issues/125), HP-SL,
  addr 19), **R&S SME** ([#137](https://github.com/TGoodhew/GPIBUtils-NG/issues/137), SCPI, addr 28),
  **R&S SMT** ([#138](https://github.com/TGoodhew/GPIBUtils-NG/issues/138), SCPI, addr 28). Batch tests (+8);
  a generic `gpibutils <device> apply -f <MHz> -l <dBm> --rf on|off` CLI (shared over all ISignalSource
  drivers). The three addr-19 HP sources (83620A/83712B/8664A) clash with the 8673B/8340B — remap on a shared
  bus. 🟡 **Verification Needed.**
- **`IFunctionGenerator` + three function generators** (issue
  [#88](https://github.com/TGoodhew/GPIBUtils-NG/issues/88) resolved: the interface lives in
  `GpibUtils.Instruments.SignalSources`, not a separate Waveforms project) — a new interface (waveform /
  frequency / Vpp amplitude / offset / output), distinct from the RF-oriented `ISignalSource`. Drivers:
  **HP 33120A** ([#106](https://github.com/TGoodhew/GPIBUtils-NG/issues/106), SCPI, addr 10),
  **Rigol DG1000Z** ([#99](https://github.com/TGoodhew/GPIBUtils-NG/issues/99), SCPI, dual-channel via
  `SelectedChannel`, GPIB-via-USB-converter, addr 2), and **HP 8116A**
  ([#118](https://github.com/TGoodhew/GPIBUtils-NG/issues/118), 1982 legacy mnemonics, addr 16; output via
  `D0`/`D1`, SRQ-on-error surfaced via serial poll + `IERR`; its waveform-select mnemonic wasn't in the manual
  excerpt so `SetWaveform` throws rather than invent one — bench follow-up). Each with a simulator + tests
  (+16); `gpibutils hp33120a|dg1000z|hp8116a …`. 🟡 **Verification Needed.**
- **HP 8903B Audio Analyzer** (issue [#131](https://github.com/TGoodhew/GPIBUtils-NG/issues/131), from the
  #70 triage) — a driver in a **new `GpibUtils.Instruments.Audio` project** implementing a **new
  `IAudioAnalyzer` interface** (P1 #86): a combined audio source + voltmeter + distortion analyzer + counter.
  Legacy keystroke-mnemonic language (`FR`/`AP`/`M`/`S`/`A`/`T` codes). Completion is a **#96 consumer**: the
  Special-Function-22 SRQ enable (`22.{mask}SP` — there is no `*SRE`) + the 8903B status-byte bit table, via
  the shared `CompletionWaiter` SRQ-edge flow; 12-byte scientific output parsed to a value. `Hp8903BSimulatedDevice`
  + 10 tests; `gpibutils hp8903b idn|init|measure`. Default `GPIB0::28::INSTR`. **Bench caveat documented:**
  the real 8903B re-triggers a measurement on every serial poll, so the poll-loop completion needs
  bench-confirmation (or a wait-for-SRQ-line + single-poll fallback). 🟡 **Verification Needed.**
- **HP 4275A Multi-Frequency LCR Meter** (issue [#109](https://github.com/TGoodhew/GPIBUtils-NG/issues/109),
  from the #70 triage) — a driver in a **new `GpibUtils.Instruments.LcrMeters` project** implementing a **new
  `ILcrMeter` interface** (P1 #83): the first impedance/LCR meter, no existing category fit. 1979 pre-SCPI
  program-code language (`A`/`C`/`F`/`T`/`E`/`I` codes); primary parameter (L/C/R/|Z|), ten spot frequencies,
  circuit mode, triggered `Measure()` → primary + secondary reading, OPEN/SHORT zero. Completion is a **#96
  consumer**: the custom `I1`/`I0` Data-Ready-SRQ enable (there is no `*SRE` register) + the fully custom
  4275A status-byte bit table, driven through the shared `CompletionWaiter` SRQ-edge flow.
  `Hp4275ASimulatedDevice` + 9 tests; `gpibutils hp4275a idn|init|measure|zero-open`. Default
  `GPIB0::17::INSTR` — **provisional** (factory switch setting unreadable in the scan; from the manual's
  sample programs). Format-A field parsing extracts the two display values, exact layout TBD at bench. 🟡
  **Verification Needed.**
- **HP 5005B Signature Multimeter** (issue [#112](https://github.com/TGoodhew/GPIBUtils-NG/issues/112), from
  the #70 triage) — a driver in `GpibUtils.Instruments.Meters` implementing a **new `ISignatureAnalyzer`
  interface** (P1 #92): a hybrid logic-signature analyzer + multimeter (frequency/totalize/time-interval/Ω/
  DCV/differential/peak-V) that fits none of the existing Meters interfaces. Legacy pre-SCPI mnemonics
  (`Fn`/`TDn`/`PCn`/`PSn`/`RS`/`ID`/`SE`/`SU`). Measurement completion is a **#96 consumer**: the vendor
  `QM<mask>`/`QM0` Service-Request-Mask enable (a custom, non-`*SRE` command) + the legacy 5005B status-byte
  bit table, driven through the shared `CompletionWaiter` SRQ-edge flow (data-ready = 0x01, error = 0x04,
  SRQ = 0x40). `Hp5005BSimulatedDevice` + 11 tests; `gpibutils hp5005b idn|init|measure|signature|error`.
  Default `GPIB0::3::INSTR`. 🟡 **Verification Needed.**
- **HP 8672A Synthesized Microwave Signal Generator** (issue
  [#126](https://github.com/TGoodhew/GPIBUtils-NG/issues/126), from the #70 triage) — a driver in
  `GpibUtils.Instruments.SignalSources` (`ISignalSource`) for the 2–18 GHz pre-488.2 microwave source (older
  sibling of the 8673B, but a distinct weighted "program-code + argument + EXECUTE" language). Frequency
  `P<kHz>Z`, power (10-dB RANGE + 1-dB VERNIER), RF on/off, and — the point of this driver — a **phase-lock
  settle that consumes the #96 `StatusOperation.ExpectBitCleared` path**: after a retune it waits (via the
  shared `CompletionWaiter`, direct-bit, **no enable mask**) for the not-phase-locked status bit to *clear*.
  `Hp8672ASimulatedDevice` + 12 tests; `gpibutils hp8672a init|cw|freq|power|rf|status`. Default
  `GPIB0::19::INSTR` (bench-remap if it clashes with the 8673B/8340B). The frequency form is reliable; the
  RANGE/VERNIER/ALC code letters are reconstructed and flagged TBD (garbled manual OCR). 🟡 **Verification Needed.**
- **HP 8591E Spectrum Analyzer** (issue [#121](https://github.com/TGoodhew/GPIBUtils-NG/issues/121), from the
  #70 Manuals-folder triage) — a driver in `GpibUtils.Instruments.Analyzers` (`ISpectrumAnalyzer`) for the
  8590 D/E/L-series legacy-mnemonic family. Center/span/RBW/VBW/sweep-time, single sweep, trace (`TRA?`) and
  markers (`MKPK HI`/`MKF?`/`MKA?`). **First consumer of the #96 `StatusModel.StatusQuery` path:** the
  pre-488.2 sweep completion reads the status byte via the `STB?` query (not a hardware serial poll) and arms
  with the legacy `RQS <mask>` mnemonic + 8590-family bit table, all through the shared `CompletionWaiter`.
  `Hp8591ESimulatedDevice` + 13 tests; `gpibutils hp8591e idn|init|sweep|trace|peak`. Default
  `GPIB0::18::INSTR` (family default). 🟡 **Verification Needed.**
- **HP 3585A/3585B Spectrum Analyzer** (issue [#108](https://github.com/TGoodhew/GPIBUtils-NG/issues/108),
  from the #70 triage) — a driver in `GpibUtils.Instruments.Analyzers` (`ISpectrumAnalyzer`) for the 1970s
  low-frequency (10 Hz–40.1 MHz) mnemonic analyzer with no `*IDN?`/`*OPC`. CF/FS/FA/FB/RB/VB/RL/ST, single
  sweep, trace (`D3`) and marker (`D1`/`D2`) dumps. Sweep completion drives the shared `CompletionWaiter`
  SRQ-edge flow with a **custom (non-`RQS`) enable command** — `CQ`/`CC` operation-complete-SRQ — proving the
  engine hardcodes no mask mnemonic (#96). `Hp3585SimulatedDevice` + 12 tests; `gpibutils hp3585
  idn|init|sweep|trace|marker`. Default `GPIB0::11::INSTR`. Targets the 3585B op-complete-SRQ path (3585A
  `T5` data-ready and limit-test SRQ are bench follow-ups; peak found in software from the trace). 🟡
  **Verification Needed.**

- **Rigol DS1054Z Oscilloscope** (issue [#27](https://github.com/TGoodhew/GPIBUtils-NG/issues/27), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — the first driver in a new `GpibUtils.Instruments.Scopes`
  project (`IOscilloscope`). Acquisition control (`:RUN`/`:STOP`/`:SINGle`/`:AUToscale`), per-channel display
  (`:CHANnel{n}:DISPlay`), timebase readback, and automatic measurements (`:MEASure:ITEM? VPP|VMAX|FREQ,CHANnel{n}`).
  The DS1054Z is a USB/LXI instrument (no GPIB), so the default resource is the legacy app's LXI address — the
  transport-neutral provider model runs the same driver over any session. `RigolDs1054ZSimulatedDevice`
  (8 tests); `gpibutils ds1054z idn|acq|channel|measure` CLI branch. 🟡 **Verification Needed.**

- **HP 3325B Synthesizer / Function Generator** (issues [#28](https://github.com/TGoodhew/GPIBUtils-NG/issues/28) +
  [#29](https://github.com/TGoodhew/GPIBUtils-NG/issues/29), consolidated from two
  [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) test apps — the 100 Hz harmonic/THD test and the
  DC-offset test) — a driver in `GpibUtils.Instruments.SignalSources`: waveform (`FU{n}` — DC/sine/square/
  triangle/positive-ramp), frequency (`FR` with HZ/KH/MH unit suffix), amplitude (`AM … VO`), DC offset
  (`OF … VO`), and amplitude calibration (`AC`). Configurable address (factory-default HP-IB address
  `GPIB0::17::INSTR` per the 3325B manual; both apps used a bench `::10::`). `Hp3325BSimulatedDevice`
  (8 tests); `gpibutils hp3325b idn|init|set` CLI branch. 🟡 **Verification Needed.**

- **HP 8350B Sweep Oscillator** (issue [#22](https://github.com/TGoodhew/GPIBUtils-NG/issues/22), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a write-only CW-source driver in
  `GpibUtils.Instruments.SignalSources`: preset (`IP`), CW frequency (`CW … MZ`), power level (`PL … DM`).
  The 8350B has no discrete RF on/off (RF is gated by the plug-in's leveling/blanking), so it is a faithful
  concrete driver rather than an `ISignalSource`. Configurable address (factory HP-IB address `GPIB0::19::INSTR`
  per the 8350B manual — note this shares the 8673B's factory 19, so remap one on a shared bench, as the 8340B
  is remapped to 20). `Hp8350BSimulatedDevice` (5 tests); `gpibutils hp8350b cw|freq|power|preset|init` CLI
  branch. 🟡 **Verification Needed.**

- **HP 5342A Microwave Frequency Counter** (issue [#32](https://github.com/TGoodhew/GPIBUtils-NG/issues/32),
  reconstructed from the 5342A manual — the [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old) source
  `.cs` was a mis-labelled DMM stub) — a driver in `GpibUtils.Instruments.Counters` for the microwave counter
  (Option 011 HP-IB): AUTO/MANUAL acquisition (`AU`/`MA`), manual center frequency (`SM…E`, integer MHz),
  resolution (`SR3`…`SR9`), and a talked frequency read guarding the over/under-level dashes sentinel.
  Bench address `GPIB0::2::INSTR` (manual programming examples; no fixed factory default — rear-panel switch).
  `Hp5342ASimulatedDevice` (10 tests); `gpibutils hp5342a idn|init|freq` CLI branch. Mnemonics reconstructed
  from the manual — flagged for bench confirmation. 🟡 **Verification Needed.**

- **HP 5351A Microwave Frequency Counter** (issue [#20](https://github.com/TGoodhew/GPIBUtils-NG/issues/20),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a driver in
  `GpibUtils.Instruments.Counters` for the single-input microwave counter: preset + clear SRQ mask
  (`SRQMASK,0`/`INIT`), sample-mode select (`SAMPLE,HOLD`/`FAST`), talked frequency read (Hz), and oven /
  reference status (`OVEN?`/`REF?`). Ported from the working `GPIBUtils/HPDevices` driver (its unwired
  frequency read is now implemented). Bench address `GPIB0::14::INSTR` (no fixed factory default — rear-panel
  switch). `Hp5351ASimulatedDevice` (7 tests); `gpibutils hp5351a idn|init|freq|status` CLI branch.
  🟡 **Verification Needed.**

- **HP/Agilent 3458A 8½-digit DMM** (issue [#31](https://github.com/TGoodhew/GPIBUtils-NG/issues/31), ported
  from [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old)) — a driver in
  `GpibUtils.Instruments.Meters` speaking the 3458A's native (non-SCPI) command language: `RESET`/`END ALWAYS`,
  `FUNC` (DCV/ACV/OHM/OHMF/DCI/ACI/FREQ/PER) with `SETACV SYNC` for AC volts, `NPLC`/`RES`, and single or
  burst triggered reads (`TARM SGL`). Identity via `ID?` (no `*IDN?`). Factory address `GPIB0::22::INSTR`
  (confirmed against the 3458A User's Guide). `Hp3458ASimulatedDevice` (11 tests);
  `gpibutils hp3458a idn|init|measure` CLI branch. 🟡 **Verification Needed.**

- **Rigol DM3058 Digital Multimeter** (issue [#26](https://github.com/TGoodhew/GPIBUtils-NG/issues/26),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — a SCPI DMM in
  `GpibUtils.Instruments.Meters` (implements `IDigitalMultimeter`) driven with one-shot `MEASure:…?` queries
  across the function set (reusing `MeasurementFunction`). The **canonical** DM3058, superseding the older
  #30 implementation — and fixing its bug where AC current was mapped to the DC-current command. Configurable
  address (factory-default GPIB address `GPIB0::7::INSTR` per the DM3058 User's Guide; the app used LXI);
  `RigolDm3058SimulatedDevice` (11 tests); `gpibutils dm3058 idn|init|measure` CLI branch. 🟡 **Verification Needed.**

- **HP 438A RF Power Meter** (issue [#33](https://github.com/TGoodhew/GPIBUtils-NG/issues/33), ported from
  [GPIBUtils-Old](https://github.com/TGoodhew/GPIBUtils-Old)) — a pre-SCPI, mnemonic-driven `IPowerMeter` in
  `GpibUtils.Instruments.Meters` for the dual-channel (A/B) 438A: preset + Log-mode (`PR`/`LG`), sensor zero
  (`ZE`), cal-factor percent (`KB…PCT`), and per-channel power read (`{A|B}P TR2`) in dBm with the meter's
  over-range error sentinel surfaced as an exception. Factory address `GPIB0::13::INSTR` (confirmed against
  the 438A manual). `Hp438ASimulatedDevice` (10 tests); `gpibutils hp438a idn|init|zero|measure` CLI branch.
  The mnemonic set is reconstructed from a partial GUI app — flagged for bench confirmation. 🟡 **Verification Needed.**

- **HP/Agilent E4418B RF Power Meter** (issue [#25](https://github.com/TGoodhew/GPIBUtils-NG/issues/25),
  ported from [GPIBUtils](https://github.com/TGoodhew/GPIBUtils)) — an `IPowerMeter` in
  `GpibUtils.Instruments.Meters`. Zeroes+calibrates the sensor (`:CAL1:ALL`), applies the cal factor for a
  carrier frequency (`:FREQ …MHZ`), and measures power in dBm (`:CONF1;:INIT;FETCh?`). Both the cal and the
  measurement complete through the shared **#43 `CompletionWaiter`** OPC→SRQ handshake (`*ESE 1`/`*SRE 32`/
  `*OPC`) — the second driver on the engine after the 53131A. New shared `IPowerMeter` contract (also used by
  the HP 438A). `HpE4418BSimulatedDevice` (8 tests); `gpibutils e4418b idn|init|cal|measure` CLI branch.
  Bench address `GPIB0::13::INSTR`. 🟡 **Verification Needed.**

- **Rigol DP832 triple-output DC Power Supply** (issue [#15](https://github.com/TGoodhew/GPIBUtils-NG/issues/15),
  ported from [DP832](https://github.com/TGoodhew/DP832)) — a SCPI `IDcPowerSupply` in
  `GpibUtils.Instruments.PowerSupplies` with per-channel control of all three outputs (CH1/CH2 30 V, CH3 5 V):
  voltage / current limit (`:SOUR{n}:VOLT`/`:CURR`), output gating (`:OUTP CH{n}`), V/I/P measurement
  (`:MEAS:...? CH{n}`), and OVP/OCP. The `IDcPowerSupply` members act on a selectable channel; explicit
  per-channel overloads cover the rest. Configurable address (default GPIB address `GPIB0::2::INSTR` per the
  DP832 User's Guide; the app used `::1::`); `RigolDp832SimulatedDevice` (10 tests);
  `gpibutils dp832 idn|init|set|output|measure` CLI branch (#45). 🟡 **Verification Needed.**

- **HP/Agilent E3633A DC Power Supply** (issue [#19](https://github.com/TGoodhew/GPIBUtils-NG/issues/19),
  ported from [E3633A-Demo](https://github.com/TGoodhew/E3633A-Demo)) — first driver in a new
  `GpibUtils.Instruments.PowerSupplies` project (`IDcPowerSupply`). Sets output voltage / current limit
  (`VOLT`/`CURR`), gates the output (`OUTP`), reads back measured voltage/current (`MEAS:VOLT?`/`MEAS:CURR?`),
  and configures over-voltage protection. Configurable address (factory-default GPIB address `GPIB0::5::INSTR`,
  confirmed against the E3633A User's Guide — the demo used a bench `::27::`); `HpE3633ASimulatedDevice` for
  hardware-free testing (11 tests); `gpibutils hpe3633a idn|init|set|output|measure` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP 53131A Universal Counter** (issues [#21](https://github.com/TGoodhew/GPIBUtils-NG/issues/21) +
  [#5](https://github.com/TGoodhew/GPIBUtils-NG/issues/5), ported from
  [GPIBUtils](https://github.com/TGoodhew/GPIBUtils) and [HP3499Demo](https://github.com/TGoodhew/HP3499Demo))
  — the **canonical** 53131A in a new `GpibUtils.Instruments.Counters` project (`IFrequencyCounter`),
  deduping the two identical `HPDevices` copies and the SCPI reader in HP3499Demo. Measures frequency on
  channels 1–3 (`CONF:FREQ (@n)`) and sets the channel-1 input impedance (50 Ω / 1 MΩ). Its measurement
  completion — the IEEE-488.2 `*ESE 1` / `*SRE 32` / `INIT;*OPC` → SRQ handshake — is driven through the
  shared **#43 `CompletionWaiter`** (direct-bit flow, `*SRE {mask}`) via `SessionStatusChannel` rather than
  a hand-rolled serial-poll loop; the data-driven `StatusModel` is the only device-specific completion
  knowledge and can move to the #41 instrument DB unchanged. A completion timeout surfaces as a typed
  `Hp53131AException`. Configurable address (factory-default GPIB address `GPIB0::3::INSTR`, confirmed
  against the 53131A Programming Guide — the legacy demo used a bench `::23::`); `Hp53131ASimulatedDevice`
  for hardware-free testing (18 tests); `gpibutils hp53131a idn|init|reset|freq` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP 3499A Switch/Control System** (issue [#4](https://github.com/TGoodhew/GPIBUtils-NG/issues/4), ported
  from [HP3499Demo](https://github.com/TGoodhew/HP3499Demo)) — a plain-SCPI switch mainframe driver in
  `GpibUtils.Instruments.Switches`. Opens/closes relay channels on the `snn` (slot + two-digit channel)
  addressing scheme (`ROUT:CLOS`/`ROUT:OPEN`/`ROUT:CLOS? (@snn)`) and enumerates installed plug-in cards
  (`SYST:CTYPE?`) — the 44472A VHF and 44476B microwave switches are plug-ins addressed via this mainframe
  scheme, not separate instruments. Configurable address (factory-default GPIB address `GPIB0::9::INSTR`,
  confirmed against the 3499A User's & Programming Guide, matching the source); `Hp3499ASimulatedDevice`
  for hardware-free testing (17 tests); `gpibutils hp3499a idn|init|cards|close|open|state` CLI branch (#45).
  🟡 **Verification Needed.**

- **HP/Agilent/Keysight 34401A Digital Multimeter** (issues
  [#36](https://github.com/TGoodhew/GPIBUtils-NG/issues/36) + [#17](https://github.com/TGoodhew/GPIBUtils-NG/issues/17),
  ported from [5440Controller](https://github.com/TGoodhew/5440Controller) and
  [HP435B-Test](https://github.com/TGoodhew/HP435B-Test)) — the **canonical** 34401A DMM in
  `GpibUtils.Instruments.Meters`, a plain SCPI `IDigitalMultimeter`. Exposes the full measurement surface:
  configure any function (DCV/ACV/DCI/ACI/2W+4W resistance/frequency/period/continuity/diode) with
  range+resolution, tune SENSe (NPLC / autorange / input impedance / autozero), TRIGger + SAMPle
  (source/count/delay), CALCulate math (null / dB / dBm / limits / average-statistics) and DISPlay text;
  read one value or a burst (`READ?`/`FETCh?`) with `DmmStatistics` (min/max/avg/sample-σ) summarizing the
  burst — consolidating the buffered recorder-output acquisition from HP435B-Test with the rich SCPI menu
  from 5440Controller. Configurable address (factory-default GPIB address `GPIB0::22::INSTR`, confirmed
  against the 34401A User's Guide); `Hp34401ASimulatedDevice` for hardware-free testing (32 tests);
  `gpibutils hp34401a idn|init|reset|read|measure|stats|selftest|errors|display` CLI branch (#45).
  🟡 **Verification Needed.**

- **Per-instrument GPIB address configuration** (issue
  [#54](https://github.com/TGoodhew/GPIBUtils-NG/issues/54)) — the bench's *actual* addresses can now be
  set and persisted, instead of being hardcoded constants or passed on every command line. A shared
  `InstrumentAddressStore` (in `GpibUtils.Common`, JSON at `%APPDATA%\GpibUtils\addresses.json` or
  `$GPIBUTILS_CONFIG`, framework-native serialization — no NuGet dependency) maps device → resource, and
  address resolution now follows the precedence **`--address` &gt; configured &gt; `DefaultResource`** (each
  driver's `DefaultResource` stays the documented manual factory default fallback). New Console commands:
  `gpibutils config address list | get <dev> | set <dev> <resource> | clear <dev>` and `config path`.
  Shared by design so the WPF shell can surface the same store once it grows per-instrument panels (11 tests).

- **`GpibUtils.Wpf`** — the Windows front-end: a WPF **MVVM** shell (issue
  [#1](https://github.com/TGoodhew/GPIBUtils-NG/issues/1)) sitting on the same shared core as the console.
  Lists the registered providers and their capabilities, discovers instruments, and runs a command against
  any provider — works with no hardware via the Simulated provider. `ViewModelBase` / `RelayCommand` MVVM
  primitives; view-model logic is unit-tested headlessly.
- **Continuous integration** — GitHub Actions workflow (`.github/workflows/ci.yml`) building and testing the
  whole solution on `windows-latest` with **no NI-VISA installed**, on every push/PR to `main`.
- **`GpibUtils.Hpgl` / `GpibUtils.Mcp`** — scaffold projects completing the foundation solution layout;
  filled in by their migrations (#42 and #41).
- **SRQ / serial-poll completion engine** (`GpibUtils.Visa.Srq`, issue
  [#43](https://github.com/TGoodhew/GPIBUtils-NG/issues/43), ported from
  [GPIB-MCP](https://github.com/TGoodhew/GPIB-MCP)) — the shared, data-driven IEEE-488.2 completion state
  machine every SRQ-based driver now builds on, so no driver hand-rolls its own serial-poll/SemaphoreSlim
  handshake. A `StatusModel` (loadable from the instrument DB) declares the status-byte bits, enable-mask
  commands and named operations; `CompletionWaiter` runs the ARM→(busy)→confirm→restore flow, choosing the
  robust **SRQ-edge** strategy when the model names a request-service bit (hardware-confirmed on the 8563E)
  or the legacy **direct-bit** poll otherwise. Decoupled from the wire via `IStatusChannel`;
  `SessionStatusChannel` bridges it onto a live `IInstrumentSession`, and an injected clock/sleep makes it
  fully headless — driven end-to-end against a virtual-clock 8560-series simulator (11 new tests). Timeouts
  and per-model `BusyConfirmMs` are kept generous to tolerate HP-IB bus-extender latency. Targets
  SRQ-enable-mask-driven completions (e.g. 8560-series sweeps); instruments with their own settled-read
  handshake (e.g. the HP 8902A) keep it.
- **HP 8902A Measuring Receiver** (issue [#9](https://github.com/TGoodhew/GPIBUtils-NG/issues/9), ported
  from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) — first `GpibUtils.Instruments.Meters`
  driver (`IMeasuringReceiver`). The **canonical** 8902A, consolidating the HP8902Measurements / GPIBUtils /
  GPIBUtils-Old implementations. Measures attenuation as relative Tuned RF Level (dB), absolute RF power
  (dBm) and signal frequency, with Normal + Frequency-Offset cal-factor tables, zero/sensor-cal, Track Mode
  (32.9SP), Average/Synchronous IF detectors, and a settled-read Data-Ready serial-poll completion handshake
  that surfaces UNCAL/RECAL and error-sentinel readings as typed `Hp8902AException`s. Configurable address
  (factory-default HP-IB address `GPIB0::14::INSTR`); `Hp8902ASimulatedDevice` for hardware-free testing
  (21 tests); `gpibutils hp8902a init|preset|status|frequency|power|level` CLI branch (#45).
  🟡 **Verification Needed.**

### Changed

- **Legacy microwave counters unified behind `ILegacyFrequencyCounter`** (issue
  [#93](https://github.com/TGoodhew/GPIBUtils-NG/issues/93)) — the HP 5342A, 5343A and 5351A standalone drivers
  now implement a shared narrow interface (`ResourceName`, `Identify`, `Initialize`, `ReadFrequency`), so the
  family can be identified/initialized/read uniformly while their model-specific controls (manual center
  frequency, resolution, range, sample mode, oven/reference status) stay on the concrete classes. They remain
  **not** `IFrequencyCounter` (that numbered-channel + selectable-impedance shape fits the SCPI 53131A). Pure
  refactor of working drivers — no behaviour change; verified by tests (+1), **no bench verification needed**.

- **`GpibUtils.Visa.Srq` `ExpectBitCleared` now works without an enable mask** (follow-up to #96, for issue
  [#126](https://github.com/TGoodhew/GPIBUtils-NG/issues/126)) — a cleared-settle operation may omit
  `EnableMask` entirely, for legacy sources (e.g. the HP 8672A) that have no `*SRE`-equivalent arm at all:
  completion is pure polling of a fault/settle bit going to 0. The `CompletionWaiter` no longer demands an
  enable mask for such operations. +1 Srq test (17 total).
- **`GpibUtils.Visa.Srq` now models pre-488.2 legacy completion** (issue
  [#96](https://github.com/TGoodhew/GPIBUtils-NG/issues/96), the cross-cutting enabler from the #70 triage) —
  still fully data-driven, no per-device code in the waiter. Two additions to the `StatusModel`:
  (1) **`StatusQuery`** — read the status byte via a device query (e.g. a legacy analyzer's `STB?`, parsed
  from a possibly-noisy ASCII reply) instead of a hardware serial poll, for instruments that expose no true
  serial poll (e.g. the 8591E); (2) **`StatusOperation.ExpectBitCleared`** — invert completion so an
  operation is done when its expect bit **clears** rather than sets, for legacy sources whose settle/operating
  bit is asserted while busy and drops when settled (e.g. the 8672A; direct-bit flow, with a busy-first
  handshake so an already-settled bit isn't read as complete). Arbitrary bit-meaning tables and custom
  (non-`RQS`) enable-mask commands were already expressible — a new headless test proves it. `IStatusChannel`
  gains `Query`; +4 Srq tests (16 total). Unblocks the legacy-mnemonic drivers below (8591E, 3585, 4275A,
  8903B, 5005B, 8672A). 🟡 **Verification Needed** (no hardware — headless sim-verified only).

- **`GpibUtils.Visa.Ni` now degrades gracefully without NI-VISA.** When the official NI/IVI VISA.NET
  assemblies aren't present at build time, the project compiles an "NI-VISA unavailable" provider stub
  (reported via the registry) instead of failing the build. The whole solution — including `Console` and
  `Wpf` — now builds and tests on a machine with **zero NI setup** (and on CI). Pass `-p:RequireNi=true`
  to hard-fail when NI is expected. No more "remove this project reference on a non-NI machine".

- **HP 8673B Synthesized Signal Generator** (issue [#8](https://github.com/TGoodhew/GPIBUtils-NG/issues/8),
  ported from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) — second `GpibUtils.Instruments.SignalSources`
  driver, an `ILocalOscillator` (2–26.5 GHz) used as the LO for the 11793A converter path: preset (`IP`),
  frequency (`FR … MZ`), level (`LE … DM`), RF on/off. Configurable address (default `GPIB0::19::INSTR`);
  `Hp8673BSimulatedDevice` for hardware-free testing; `gpibutils hp8673b cw|freq|power|rf|preset|init`
  CLI branch (#45). The **canonical** 8673B driver, consolidating the implementations in HP8902Measurements,
  HP8273BLLMTest and GPIBUtils. 🟡 **Verification Needed.**
- **`GpibUtils.Instruments.SignalSources`** — signal-source driver category with a shared `ISignalSource`
  contract. **HP 8340B Synthesized Sweeper** (issue [#7](https://github.com/TGoodhew/GPIBUtils-NG/issues/7),
  ported from [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)) driven as a CW source: preset
  (`IP`), frequency (`CW … MZ`), power (`PL … DB`), and RF on/off (`RF1`/`RF0`), with culture-invariant
  formatting. Runs on the shared `IInstrumentSession`; address configurable (default `GPIB0::20::INSTR`).
  Includes `Hp8340BSimulatedDevice` for hardware-free testing. This is the **canonical** 8340B driver for
  the related app issues [#16](https://github.com/TGoodhew/GPIBUtils-NG/issues/16) /
  [#34](https://github.com/TGoodhew/GPIBUtils-NG/issues/34) to build on. 🟡 **Verification Needed.**
- **`gpibutils hp8340b …` CLI branch** (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45))
  — `cw` (one-shot preset+freq+power+RF-on) / `freq` / `power` / `rf` / `preset` / `init`.
- **`GpibUtils.Instruments.Switches`** — first instrument driver category. **HP 11713A Attenuator/Switch
  Driver** (issue [#6](https://github.com/TGoodhew/GPIBUtils-NG/issues/6), ported from
  [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator)): dB→A/B relay-string solver
  (`Hp11713ACommandBuilder`), configurable attenuator wiring (`AttenuatorConfig`, default 8494+8496 =
  0–121 dB in 1 dB steps), independent S9/S0 switches, and a software state shadow (the 11713A is
  listen-only). Runs on the shared vendor-neutral `IInstrumentSession`; GPIB address is configurable
  (default `GPIB0::28::INSTR`). Includes `Hp11713ASimulatedDevice`, an in-memory model that decodes the
  A/B data strings back into relay state for hardware-free testing. 🟡 **Verification Needed.**
- **`gpibutils hp11713a …` CLI branch** (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45))
  — hierarchical, self-documenting commands `set` / `engage` / `zero` / `switch9` / `switch0` / `raw`,
  with shared `--provider` / `--address` / `--timeout` options (plus `--config` / `--invert-sense`) at
  the leaf. Establishes the per-instrument CLI pattern for future drivers.
- **`SimulatedInstrument.WriteObserver`** (foundation) — a hook that reports every write, so a simulated
  **listen-only** instrument can track the state it is driven into. Enables end-to-end,
  no-hardware verification of write-only drivers.

Foundation (issue [#1](https://github.com/TGoodhew/GPIBUtils-NG/issues/1)) — scaffolding and the
shared transport:

### Added (foundation)

- **`GpibUtils.Visa`** — vendor-neutral, pluggable GPIB transport (`net472`): `IGpibProvider` /
  `IInstrumentSession` abstractions, capability reporting, the `GpibProviders` registry, extension
  stubs (`Keysight-VISA` / `Prologix` / `AR488`) and an in-memory `Simulated` provider. Has **no
  vendor dependency, so it builds anywhere**. Provider selection via code or the
  `GPIBUTILS_GPIB_PROVIDER` env var (default `NI-VISA`).
- **`GpibUtils.Visa.Ni`** — `NI-VISA` (default) and native `NI-488.2` (opt-in
  `-p:DefineConstants=NI4882`) providers built against the **official NI / IVI VISA.NET assemblies**
  referenced by `HintPath` from the local NI-VISA install (never vendored; auto-registered by
  reflection when deployed).
- **`GpibUtils.Common`** — shared helpers, starting with a consolidated and hardened
  `ToEngineeringFormat`.
- **`GpibUtils.Console`** — runnable `Spectre.Console.Cli` app `gpibutils` with base commands
  `providers` / `discover` / `query` / `idn`; `-e <unit>` engineering-formats numeric replies. Runs
  hardware-free against the `Simulated` provider.
- **Tests** — xUnit suites for `GpibUtils.Visa` and `GpibUtils.Common` (**21 tests** green: 12 Visa +
  9 Common).
- **Docs** — [`docs/implementing-a-gpib-provider.md`](docs/implementing-a-gpib-provider.md) provider-authoring
  guide; `SharedMemory.md` portable project-status handoff.

### Project decisions

- **Language:** C# (other languages allowed in subprojects); primary language enforced via
  `.gitattributes` (Linguist) vendoring bulk data.
- **Target framework:** .NET Framework 4.7.2 (`net472`) for all projects, including the WPF front-end.
  Built with the .NET 10 SDK via `Microsoft.NETFramework.ReferenceAssemblies` (no full targeting pack
  required).
- **VISA layer:** pluggable provider model; NI providers use the **official NI assemblies** via
  `HintPath` (the Kelary community NuGet was dropped; the official NI NuGet is net6.0-only and
  unusable on `net472`).
- **UI split:** Console = Spectre.Console, Windows = WPF; the core/driver libraries carry no UI
  dependency. WinForms sources migrate to WPF.
- **Reference architecture:** the [HP-Attenuator](https://github.com/TGoodhew/HP-Attenuator) repo.
- **CLI-first (issue [#45](https://github.com/TGoodhew/GPIBUtils-NG/issues/45)):** every instrument
  must be fully driveable from a hierarchical CLI with self-documenting `--help` at every level.

[Unreleased]: https://github.com/TGoodhew/GPIBUtils-NG/commits/main
