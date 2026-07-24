# Hardware verification status

**Master status board for real-hardware verification.** This is the portable, in-repo mirror of the
pinned GitHub tracking issue [#46](https://github.com/TGoodhew/GPIBUtils-NG/issues/46) and the **single live
tracker** for what still needs a bench check. Because development currently happens **without access to the
physical instruments**, drivers are merged to `main` on simulator/unit-test green and then wait here for
bench confirmation.

> **Coverage:** this board lists **every merged driver** (one row per `verify/<n>-<slug>` tag), across the
> whole consolidation — the pre-#70 ports, the #70 Manuals-folder migration, and later additions (M9811,
> ThinkJet). It supersedes the scoped [#97](https://github.com/TGoodhew/GPIBUtils-NG/issues/97) "Needs
> Verification" epic (the #70 migration index, now complete) for live tracking. Rows with a full note were
> authored at merge time; the concise rows point to the per-issue bench checklist.

## The no-hardware workflow

1. **Branch** per issue: `feat/<issue#>-<slug>` (e.g. `feat/6-hp11713a`).
2. **Build it hardware-free** — port the driver onto the shared transport, add a simulator/mock and
   unit tests, and wire the CLI branch (issue #45). It must build and pass tests under the
   **Simulated** provider.
3. **Merge to `main`** on simulator/unit-test green. **HW verification is _not_ a merge gate** — this
   is what lets the next driver build on it immediately while away from the bench.
4. **Do _not_ close the issue.** Instead:
   - Label it **`verification-needed`**.
   - Add the bench checklist (below) to the issue.
   - Add a row to the table here and to the pinned tracking issue.
   - Tag the merge commit **`verify/<issue#>-<instrument>`** so the exact merged state can be checked
     out at the bench later, even after `main` has advanced.
5. **At the bench** (hardware available): run the checklist against the real instrument, record the
   result here and on the issue (pass/fail, firmware/serial, date, notes).
6. **Close** the issue only after a ✅ pass — or open a follow-up if hardware reveals a discrepancy.

## Status legend

| State | Meaning |
|---|---|
| ⬜ Not built | No driver code merged yet. |
| 🟡 Verification Needed | Merged to `main`, simulator/tests green, awaiting real hardware. |
| ✅ HW-Verified | Confirmed against the physical instrument. |
| ⚠️ Discrepancy | Hardware behaviour differed from the simulator/port — see notes / follow-up. |

## Board

| Issue | Instrument | Verify tag | HW required | Status | FW / serial | Date | Notes |
|---|---|---|---|---|---|---|---|
| [#6](https://github.com/TGoodhew/GPIBUtils-NG/issues/6) | HP 11713A Attenuator/Switch Driver | `verify/6-hp11713a` | 11713A + step attenuators (8494/8496), GPIB | 🟡 Verification Needed | — | — | Merged simulator-green; 25 unit tests. Confirm relay data strings against real hardware. |
| [#7](https://github.com/TGoodhew/GPIBUtils-NG/issues/7) | HP 8340B Synthesized Sweeper | `verify/7-hp8340b` | 8340B, GPIB; power meter / counter to confirm output | 🟡 Verification Needed | — | — | Merged simulator-green; 14 unit tests. Confirm CW frequency / power / RF gating on real output. |
| [#8](https://github.com/TGoodhew/GPIBUtils-NG/issues/8) | HP 8673B Synthesized Signal Generator | `verify/8-hp8673b` | 8673B, GPIB; counter / power meter (2-26.5 GHz) | 🟡 Verification Needed | — | — | Merged simulator-green; 13 unit tests. Confirm FR/LE mnemonics + RF gating across the band on real output. |
| [#9](https://github.com/TGoodhew/GPIBUtils-NG/issues/9) | HP 8902A Measuring Receiver | `verify/9-hp8902a` | 8902A + sensor, GPIB (addr 14); signal source + attenuator | 🟡 Verification Needed | — | — | Merged simulator-green; 21 unit tests. Confirm settled-read Data-Ready serial poll, cal-factor tables, Tuned RF Level / RF power. |
| [#36](https://github.com/TGoodhew/GPIBUtils-NG/issues/36) | HP 34401A Digital Multimeter | `verify/36-hp34401a` | 34401A, GPIB (addr 22); a known DC source / calibrator | 🟡 Verification Needed | — | — | Merged simulator-green; 32 unit tests. Confirm CONF/SENSe/TRIGger surface, single + burst `READ?`, and NPLC/range on real hardware. Consolidates #17 + #36. |
| [#21](https://github.com/TGoodhew/GPIBUtils-NG/issues/21) | HP 53131A Universal Counter | `verify/21-hp53131a` | 53131A, GPIB (factory addr 3; bench 23); a stable RF/CW source | 🟡 Verification Needed | — | — | Merged simulator-green; 18 unit tests. Confirm the `*ESE 1`/`*SRE 32`/`INIT;*OPC` completion via the #43 engine, per-channel `CONF:FREQ (@n)`, `INP:IMP`. Consolidates #21 + #5. **Confirm the bench address (demo used 23).** |
| [#4](https://github.com/TGoodhew/GPIBUtils-NG/issues/4) | HP 3499A Switch/Control System | `verify/4-hp3499a` | 3499A + plug-ins (44472A VHF, 44476B microwave), GPIB (addr 9) | 🟡 Verification Needed | — | — | Merged simulator-green; 17 unit tests. Confirm `ROUT:CLOS`/`OPEN`/`CLOS?` relay control on the `snn` channel scheme and `SYST:CTYPE?` card inventory against real plug-ins. |
| [#19](https://github.com/TGoodhew/GPIBUtils-NG/issues/19) | HP E3633A DC Power Supply | `verify/19-hpe3633a` | E3633A, GPIB (factory addr 5; demo used 27) | 🟡 Verification Needed | — | — | Merged simulator-green; 11 unit tests. Confirm `VOLT`/`CURR`/`OUTP` set + `MEAS:VOLT?`/`MEAS:CURR?` readback. **Confirm the bench address (demo used 27).** |
| [#15](https://github.com/TGoodhew/GPIBUtils-NG/issues/15) | Rigol DP832 DC Power Supply | `verify/15-dp832` | DP832, GPIB (default addr 2; app used 1) or LXI/USB | 🟡 Verification Needed | — | — | Merged simulator-green; 10 unit tests. Confirm per-channel `:SOUR{n}:VOLT/CURR`, `:OUTP CH{n}`, `:MEAS:...? CH{n}`, OVP/OCP on all 3 channels. |
| [#25](https://github.com/TGoodhew/GPIBUtils-NG/issues/25) | HP/Agilent E4418B Power Meter | `verify/25-e4418b` | E4418B + sensor, GPIB (bench addr 13) | 🟡 Verification Needed | — | — | Merged simulator-green; 8 unit tests. Confirm the `:CAL1:ALL` zero/cal and `:CONF1;:INIT;FETCh?` measure both complete via the #43 OPC→SRQ handshake; `:FREQ …MHZ` cal factor; dBm reading. Confirm bench address. |
| [#33](https://github.com/TGoodhew/GPIBUtils-NG/issues/33) | HP 438A Power Meter | `verify/33-hp438a` | 438A + sensor(s), GPIB (factory addr 13) | 🟡 Verification Needed | — | — | Merged simulator-green; 10 unit tests. **Mnemonics reconstructed from a partial GUI app — confirm `CS`/`PR`/`LG`/`ZE`/`{A\|B}P TR2`/`KB…PCT` and the over-range sentinel against the real 438A.** |
| [#26](https://github.com/TGoodhew/GPIBUtils-NG/issues/26) | Rigol DM3058 DMM | `verify/26-dm3058` | DM3058, GPIB (factory addr 7) or LXI | 🟡 Verification Needed | — | — | Merged simulator-green; 11 unit tests. Confirm one-shot `MEAS:VOLT/CURR/RES?` on real hardware. Consolidates #26 (supersedes #30 + its AC-current bug). Legacy app used LXI. |
| [#31](https://github.com/TGoodhew/GPIBUtils-NG/issues/31) | HP 3458A 8.5-digit DMM | `verify/31-hp3458a` | 3458A, GPIB (factory addr 22) | 🟡 Verification Needed | — | — | Merged simulator-green; 11 unit tests. Confirm the native `RESET`/`FUNC`/`NPLC`/`RES`/`SETACV SYNC`/`TARM SGL` flow and single/burst reads. |
| [#20](https://github.com/TGoodhew/GPIBUtils-NG/issues/20) | HP 5351A Microwave Counter | `verify/20-hp5351a` | 5351A + microwave source, GPIB (app used 14) | 🟡 Verification Needed | — | — | Merged simulator-green; 7 unit tests. Confirm `SRQMASK,0`/`INIT`/`SAMPLE,HOLD\|FAST`, the talked frequency read, and `OVEN?`/`REF?`. Confirm bench address. |
| [#32](https://github.com/TGoodhew/GPIBUtils-NG/issues/32) | HP 5342A Microwave Counter | `verify/32-hp5342a` | 5342A (Opt 011 HP-IB) + microwave source, GPIB (manual examples use 2) | 🟡 Verification Needed | — | — | Merged simulator-green; 10 unit tests. **Reconstructed entirely from the manual (the source was a DMM stub) — confirm `RE`/`AU`/`MA`/`SR{n}`/`SM…E`, the talked read + dashes sentinel, and the SRQ bit-7 completion.** |
| [#22](https://github.com/TGoodhew/GPIBUtils-NG/issues/22) | HP 8350B Sweep Oscillator | `verify/22-hp8350b` | 8350B + RF plug-in, GPIB (factory addr 19; shares 8673B's 19 — remap one) | 🟡 Verification Needed | — | — | Merged simulator-green; 5 unit tests. Confirm `IP`/`CW … MZ`/`PL … DM` for CW frequency + power. Write-only (no RF on/off). |
| [#28](https://github.com/TGoodhew/GPIBUtils-NG/issues/28) | HP 3325B Synthesizer | `verify/28-hp3325b` | 3325B, GPIB (factory addr 17; apps used 10) | 🟡 Verification Needed | — | — | Merged simulator-green; 8 unit tests. Confirm `FU{n}` waveform, `FR`/`AM`/`OF` + unit suffixes, and `AC` amplitude cal. Consolidates #28 (harmonic/THD) + #29 (DC offset). |
| [#27](https://github.com/TGoodhew/GPIBUtils-NG/issues/27) | Rigol DS1054Z Oscilloscope | `verify/27-ds1054z` | DS1054Z, USB/LXI (no GPIB) | 🟡 Verification Needed | — | — | Merged simulator-green; 8 unit tests. Confirm `:RUN`/`:STOP`/`:SINGle`/`:AUToscale`, channel display, and `:MEASure:ITEM?` on real hardware over USB/LAN. |
| [#10](https://github.com/TGoodhew/GPIBUtils-NG/issues/10) | HP/Agilent 8563E Spectrum Analyzer + 85620A Mass Memory Module | `verify/10-hp85620a` | Real instrument; see #10 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #10. |
| [#11](https://github.com/TGoodhew/GPIBUtils-NG/issues/11) | Keysight/Agilent E4438C ESG | `verify/11-e4438c` | Real instrument; see #11 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #11. |
| [#12](https://github.com/TGoodhew/GPIBUtils-NG/issues/12) | Agilent E4406A VSA Transmitter Tester | `verify/12-e4406a` | Real instrument; see #12 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #12. |
| [#13](https://github.com/TGoodhew/GPIBUtils-NG/issues/13) | HP/Agilent 8560E Spectrum Analyzer | `verify/13-hp8560e` | Real instrument; see #13 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #13. |
| [#35](https://github.com/TGoodhew/GPIBUtils-NG/issues/35) | Fluke 5440A / 5440B DC Voltage Calibrator | `verify/35-fluke5440a` | Real instrument; see #35 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #35. |
| [#38](https://github.com/TGoodhew/GPIBUtils-NG/issues/38) | HP 7090A Measurement Plotting System (+7475A/7550A) | `verify/38-plotters` | HP 7090A/7475A/7550A, GPIB; see #38 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #38. **On the bench (confirmed 2026-07-24).** Driver default `GPIB0::6::INSTR` — confirm the plotter's front-panel address. Renders via `GpibUtils.Hpgl`; the first real HP-GL round-trip also exercises the #225 NI byte-transparency fix. |
| [#99](https://github.com/TGoodhew/GPIBUtils-NG/issues/99) | Rigol DG1000Z Series function/arbitrary waveform generator | `verify/99-dg1000z` | Real instrument; see #99 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #99. |
| [#100](https://github.com/TGoodhew/GPIBUtils-NG/issues/100) | Tektronix MSO3000/DPO3000 Series | `verify/100-dpo3000` | Real instrument; see #100 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #100. |
| [#101](https://github.com/TGoodhew/GPIBUtils-NG/issues/101) | Tektronix DPO4034/DPO4000/MSO4000 series | `verify/101-dpo4000` | Real instrument; see #101 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #101. |
| [#102](https://github.com/TGoodhew/GPIBUtils-NG/issues/102) | Rigol DSA800-series | `verify/102-dsa800` | Real instrument; see #102 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #102. |
| [#103](https://github.com/TGoodhew/GPIBUtils-NG/issues/103) | Agilent/Keysight E4436B ESG-D Signal Generator | `verify/103-e4436b` | Real instrument; see #103 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #103. |
| [#104](https://github.com/TGoodhew/GPIBUtils-NG/issues/104) | HP 8508A Vector Voltmeter | `verify/104-hp8508a` | Real instrument; see #104 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #104. |
| [#105](https://github.com/TGoodhew/GPIBUtils-NG/issues/105) | HP 3245A Universal Source | `verify/105-hp3245a` | Real instrument; see #105 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #105. |
| [#106](https://github.com/TGoodhew/GPIBUtils-NG/issues/106) | HP 33120A Function/Arbitrary Waveform Generator | `verify/106-hp33120a` | Real instrument; see #106 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #106. |
| [#107](https://github.com/TGoodhew/GPIBUtils-NG/issues/107) | HP 3335A Synthesizer/Level Generator | `verify/107-hp3335a` | Real instrument; see #107 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #107. |
| [#108](https://github.com/TGoodhew/GPIBUtils-NG/issues/108) | HP 3585A/3585B | `verify/108-hp3585` | Real instrument; see #108 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #108. |
| [#109](https://github.com/TGoodhew/GPIBUtils-NG/issues/109) | HP 4275A Multi-Frequency LCR Meter | `verify/109-hp4275a` | Real instrument; see #109 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #109. |
| [#110](https://github.com/TGoodhew/GPIBUtils-NG/issues/110) | HP 436A | `verify/110-hp436a` | Real instrument; see #110 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #110. |
| [#111](https://github.com/TGoodhew/GPIBUtils-NG/issues/111) | HP 437B Power Meter | `verify/111-hp437b` | Real instrument; see #111 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #111. |
| [#112](https://github.com/TGoodhew/GPIBUtils-NG/issues/112) | HP 5005B Signature Multimeter | `verify/112-hp5005b` | Real instrument; see #112 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #112. |
| [#113](https://github.com/TGoodhew/GPIBUtils-NG/issues/113) | HP 53310A | `verify/113-hp53310a` | Real instrument; see #113 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #113. |
| [#114](https://github.com/TGoodhew/GPIBUtils-NG/issues/114) | HP 5343A Microwave Frequency Counter | `verify/114-hp5343a` | Real instrument; see #114 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #114. |
| [#115](https://github.com/TGoodhew/GPIBUtils-NG/issues/115) | HP/Agilent 54622A/54622D | `verify/115-hp54622` | Real instrument; see #115 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #115. |
| [#116](https://github.com/TGoodhew/GPIBUtils-NG/issues/116) | Agilent/HP 54845A Infiniium Oscilloscope | `verify/116-hp54845a` | Real instrument; see #116 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #116. |
| [#117](https://github.com/TGoodhew/GPIBUtils-NG/issues/117) | HP/Agilent 6625A System DC Power Supply | `verify/117-hp6625a` | Real instrument; see #117 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #117. |
| [#118](https://github.com/TGoodhew/GPIBUtils-NG/issues/118) | HP 8116A | `verify/118-hp8116a` | Real instrument; see #118 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #118. |
| [#119](https://github.com/TGoodhew/GPIBUtils-NG/issues/119) | HP/Agilent 83620A Synthesized Swept-Signal Generator | `verify/119-hp83620a` | Real instrument; see #119 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #119. |
| [#120](https://github.com/TGoodhew/GPIBUtils-NG/issues/120) | HP/Agilent 83712B Synthesized CW Generator | `verify/120-hp83712b` | Real instrument; see #120 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #120. |
| [#121](https://github.com/TGoodhew/GPIBUtils-NG/issues/121) | HP/Agilent 8591E Spectrum Analyzer | `verify/121-hp8591e` | Real instrument; see #121 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #121. |
| [#122](https://github.com/TGoodhew/GPIBUtils-NG/issues/122) | HP 8656A/8656B Synthesized Signal Generator | `verify/122-hp8656` | Real instrument; see #122 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #122. |
| [#123](https://github.com/TGoodhew/GPIBUtils-NG/issues/123) | HP 8657B Synthesized Signal Generator | `verify/123-hp8657b` | Real instrument; see #123 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #123. |
| [#124](https://github.com/TGoodhew/GPIBUtils-NG/issues/124) | HP 8663A Synthesized Signal Generator | `verify/124-hp8663a` | Real instrument; see #124 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #124. |
| [#125](https://github.com/TGoodhew/GPIBUtils-NG/issues/125) | HP 8664A Synthesized Signal Generator | `verify/125-hp8664a` | Real instrument; see #125 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #125. |
| [#126](https://github.com/TGoodhew/GPIBUtils-NG/issues/126) | HP 8672A Synthesized Microwave Signal Generator | `verify/126-hp8672a` | Real instrument; see #126 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #126. |
| [#127](https://github.com/TGoodhew/GPIBUtils-NG/issues/127) | HP 8711C/8712C/8713C/8714C RF Network Analyzer family | `verify/127-hp8714` | Real instrument; see #127 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #127. |
| [#128](https://github.com/TGoodhew/GPIBUtils-NG/issues/128) | HP 8720C | `verify/128-hp8720c` | Real instrument; see #128 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #128. |
| [#129](https://github.com/TGoodhew/GPIBUtils-NG/issues/129) | HP 8757D Scalar Network Analyzer | `verify/129-hp8757d` | Real instrument; see #129 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #129. |
| [#130](https://github.com/TGoodhew/GPIBUtils-NG/issues/130) | HP 8901A/8901B | `verify/130-hp8901` | Real instrument; see #130 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #130. |
| [#131](https://github.com/TGoodhew/GPIBUtils-NG/issues/131) | HP 8903B Audio Analyzer | `verify/131-hp8903b` | Real instrument; see #131 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #131. |
| [#132](https://github.com/TGoodhew/GPIBUtils-NG/issues/132) | HP 8970B Noise Figure Meter | `verify/132-hp8970b` | Real instrument; see #132 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #132. |
| [#133](https://github.com/TGoodhew/GPIBUtils-NG/issues/133) | Keithley 2015/2015P THD Multimeter | `verify/133-keithley2015` | Real instrument; see #133 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #133. |
| [#134](https://github.com/TGoodhew/GPIBUtils-NG/issues/134) | Keithley 2400 | `verify/134-keithley2400` | Real instrument; see #134 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #134. |
| [#135](https://github.com/TGoodhew/GPIBUtils-NG/issues/135) | LeCroy LC574A | `verify/135-lc574a` | Real instrument; see #135 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #135. |
| [#136](https://github.com/TGoodhew/GPIBUtils-NG/issues/136) | Agilent N9320A Spectrum Analyzer | `verify/136-n9320a` | Real instrument; see #136 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #136. |
| [#137](https://github.com/TGoodhew/GPIBUtils-NG/issues/137) | Rohde & Schwarz SME Signal Generator family | `verify/137-rs-sme` | Real instrument; see #137 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #137. |
| [#138](https://github.com/TGoodhew/GPIBUtils-NG/issues/138) | R&S SMT | `verify/138-rs-smt` | Real instrument; see #138 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #138. |
| [#139](https://github.com/TGoodhew/GPIBUtils-NG/issues/139) | Tektronix TDS784C/TDS784D | `verify/139-tds784` | Real instrument; see #139 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #139. |
| [#140](https://github.com/TGoodhew/GPIBUtils-NG/issues/140) | LeCroy WaveRunner 6000 Series | `verify/140-waverunner6000` | Real instrument; see #140 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #140. |
| [#164](https://github.com/TGoodhew/GPIBUtils-NG/issues/164) | Maynuo M9811 programmable DC electronic load | `verify/164-m9811` | M9811 load, serial (ASRL / Modbus RTU); see #164 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #164. |
| [#166](https://github.com/TGoodhew/GPIBUtils-NG/issues/166) | HP 2225 ThinkJet printer | `verify/166-thinkjet` | HP 2225 ThinkJet, GPIB; see #166 | 🟡 Verification Needed | — | — | Merged simulator-green; bench checklist on #166. **On the bench (confirmed 2026-07-24).** Driver default `GPIB0::1::INSTR` — confirm the address, and that the unit is the HP-IB **2225A** variant (2225B/C/D are Centronics/serial, not GPIB). Renders via `GpibUtils.Pcl`; the binary PCL round-trip also exercises the #225 NI byte-transparency fix. |
| [#174](https://github.com/TGoodhew/GPIBUtils-NG/pull/174) | Cross-instrument **verification harness** (`verify harness` / `verify source`) | `verify/174-harness` | A DUT **plus** at least one reference instrument (e.g. 8340B + 8902A) | 🟡 Verification Needed | — | — | Not a driver — the tool used to *run* verification. Now **rehearsable hardware-free** via the simulated bench ([#179](https://github.com/TGoodhew/GPIBUtils-NG/issues/179)), but that coupling is exact/PASS-by-construction, so it proves the plumbing only. First bench use doubles as its own verification. |
| [#172](https://github.com/TGoodhew/GPIBUtils-NG/issues/172) | Interactive **TUI** front-end + DMM parity (CLI · WPF · TUI) | `verify/172-tui` | HP 34401A (the DMM panel / `monitor` verb) | 🟡 Verification Needed | — | — | Core logic is unit-tested headless; the live DMM dashboard and streaming `monitor` need a real 34401A to confirm update cadence and readings. |

## Per-instrument bench checklist (template)

Copy this into the issue when it moves to 🟡 Verification Needed, and fill it in at the bench:

```
### HW verification — <instrument> (issue #<n>)
Environment: provider <NI-VISA|…>, address <GPIB0::N::INSTR>, firmware <…>, serial <…>, date <YYYY-MM-DD>

- [ ] Discovery / *IDN? returns the expected model
- [ ] Each CLI command drives the instrument as designed (list them)
- [ ] Readings match the simulator's assumptions (units, scaling, engineering format)
- [ ] SRQ / serial-poll completion behaves (if the driver uses it)
- [ ] Error/timeout paths handled gracefully (bad address, no response)
- [ ] No hardcoded addresses; `--provider` / `--address` / `--timeout` honoured

Result: ✅ pass  /  ⚠️ discrepancy (describe)  /  ❌ fail (describe)
```
