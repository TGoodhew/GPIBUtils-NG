# Manuals-folder triage (issue #70)

Full triage of the instrument-manual PDFs in `C:\Users\Tony\OneDrive\Documents\Manuals`, the
authority folder for default GPIB addresses and the inventory of *what hardware exists / is programmable*.
This is deliverable #1 of [#70](https://github.com/TGoodhew/GPIBUtils-NG/issues/70): one row per manual →
instrument → programmable? → disposition.

**Scope of "programmable":** remotely controllable over VISA / IEEE-488.2 — GPIB/HP-IB, or SCPI over
USB-TMC / LXI-LAN / RS-232. Purely manual/analog bench gear, service-only manuals with no bus, accessories
(cables, sensors, attenuators, fixtures, plug-in modules), app notes, software manuals, and consumer/household
items are **not** programmable → `n-a`.

## Method

The folder holds **571 PDFs** (grown from the ~424 estimated when the original #70 triage ran; new manuals
have been added since — e.g. the HP 8757D set that unblocked [#129](https://github.com/TGoodhew/GPIBUtils-NG/issues/129)).
Files were swept in 8 parallel passes; opaque scanner-named files (`20090818…​.pdf`, `13f12om3e.pdf`, …) were
identified by extracting their first pages with `pdftotext`. De-dupe is **by instrument**, not by file — most
programmable instruments have several manuals (operating / service / programming / calibration / quick-ref).

## Summary

- **Programmable instruments found:** every one is already **covered** — migrated this arc (#70), migrated
  pre-#70 (epic #44), or backlogged/linked (5440A, E4438C, 8560E, E4406A, 85620A, HP-GL plotters). No blocked
  drivers remain.
- **Not programmable (`n-a`):** the large majority — analog HP 3xx/4xx voltmeters/meters/oscillators, power
  sensors/attenuators/couplers, printers & disc drives, HP-IB bus extenders/gateways, app notes, HP BASIC /
  VEE / VISA software, and a long tail of consumer manuals (appliances, cameras, a Ford F-150, …) that happen
  to live in the same folder.
- **Newly-discovered programmable instruments (NOT previously tracked):**
  | Instrument | Kind | Action | Ownership |
  |---|---|---|---|
  | **Maynuo M9811** | Programmable DC electronic load (M97xx series, SCPI-like) | Issue #164 — new `IElectronicLoad` interface | **Owned** — build |
  | ~~Keithley 2000~~ | 6.5-digit GPIB DMM (migrated 2015 minus THD) | #163 closed | **Not owned** (only the 2015-THD) |
  | **HP 2225 ThinkJet** | HP-IB printer (text + PCL raster) — an **output device** | Issue #166 — new `IPrinter` output-device surface | **Owned** — build |
- **Correction (2026-07-18):** the **HP 7090A** was initially flagged NEW, but it is **already covered** by the
  shipped `HpPlotter` driver (`HpPlotterModel.Hp7090A`) alongside the 7475A/7550A the user owns. Only its
  analog-input digitize/recorder side is unsurfaced (optional).
- **Borderline / not filed:**
  - **Keysight U1253B** — handheld DMM, but only over a proprietary IR-USB serial link, not GPIB/VISA.
  - **Symmetricom GPS 149** GPSDO (hobby-grade RS-232 help-shell) and **HP 310A** (image-only scan, presumed
    vintage analog wave analyzer) — could not confirm a standard bus; left `unknown`.

## Triage table

`programmable`: yes / no / unknown. `disposition`: `covered` (migrated or backlogged — see note), `n-a` (not
programmable / out of scope), `NEW` (programmable, not previously tracked), `unknown` (could not identify).

| Filename | Instrument | Prog? | Disposition | Note |
|---|---|---|---|---|
| 007-10-Rev-K-A007A-N-Gage-Kit.pdf | Agilent A007A N-gage calibration kit | no | n-a | Accessory/verification kit, no bus |
| 02225-90079_ThinkJet_ServiceManual.pdf | HP 2225 ThinkJet printer | no | n-a | Printer service manual |
| 03400A-OSM-90013.pdf | HP 3400A True RMS voltmeter | no | n-a | Analog voltmeter, no GPIB |
| 08340-90243.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B migrated; operating/programming manual |
| 08340-90243CH16.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B dup (chapter 16) |
| 08340-90245-serv-2.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B dup (service) |
| 08340-90245-serv.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B dup (service) |
| 08340-90245.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B dup (service) |
| 08340-90336-cal-v2.pdf | HP 8340A/B synthesized sweeper | yes | covered | 8340B dup (calibration) |
| 08481-90174.pdf | HP 8481A power sensor | no | n-a | Power sensor, no bus |
| 08481-90175.pdf | HP 8481 power sensor | no | n-a | Power sensor, no bus |
| 10 Hints for Making Better Network Analyzer Measurements.pdf | HP/Agilent app note | no | n-a | Application note |
| 10101480 English_f7389734-3_.pdf | Char-Broil Oil-Less Turkey Fryer | no | n-a | Consumer appliance |
| 105-man220.pdf | TestEquity Model 105 temperature chamber | unknown | n-a | Watlow controller (Modbus/serial); out of GPIB scope |
| 11667A.pdf | HP 11667A power splitter | no | n-a | Passive accessory |
| 11683A.pdf | HP 11683A range calibrator | no | n-a | Power-meter cal accessory |
| 11713A-OSM.pdf | HP 11713A attenuator/switch driver | yes | covered | 11713A migrated (pre-#70) |
| 11713A.pdf | HP 11713A attenuator/switch driver | yes | covered | 11713A dup |
| 11721A Operating and Service.pdf | HP 11721A frequency doubler | no | n-a | Accessory, no bus |
| 11793A.pdf | HP 11793A microwave converter | no | n-a | Noise-figure system accessory |
| 13f12om3e.pdf | Ford F-150 (2013) owner manual | no | n-a | Vehicle manual |
| 16902B Service.pdf | Agilent 16902B logic analysis system | no | n-a | N/A per covered.md (no GPIB/SCPI) |
| 16902Service.pdf | Agilent 16902B logic analysis system | no | n-a | 16902B dup (N/A) |
| 175_____umeng0200.pdf | Fluke 175/177/179 handheld DMM | no | n-a | Handheld DMM, no computer interface |
| 1983-05.pdf | HP Journal May 1983 | no | n-a | Journal/article |
| 1P053-00_04_EM027BS013_20140819.pdf | Pervasive Displays e-paper panel | no | n-a | Display panel datasheet |
| 2000-902-01 (B - Mar 1997)(Repair).pdf | Keithley 2000/2015 DMM repair manual | yes | covered | Shared 2000-series repair manual (2015 migrated; see NEW Keithley 2000) |
| 20090818-to-20090826 (45 files).pdf | R&S SME signal generator | yes | covered | 45 opaque scanner-named pages of the SME service manual (SME migrated) |
| 20090826150448061.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826150757552.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826151300694.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826151552883.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826152340269.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826152611985.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826152857039.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826153349404.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826153649351.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 20090826153829278.pdf | R&S SME signal generator (service scan) | yes | covered | SME service-manual scan page |
| 200CD-EARLY-OSM.pdf | HP 200CD Audio Oscillator | no | n-a | Analog RC oscillator, no bus |
| 200CD-LATE-OSM.pdf | HP 200CD Audio Oscillator | no | n-a | Dup variant |
| 200CD.pdf | HP 200CD Audio Oscillator | no | n-a | Dup variant |
| 2015THD Service.pdf | Keithley 2015 THD multimeter | yes | covered | Keithley 2015 (service) |
| 2015THDUser.pdf | Keithley 2015 THD multimeter | yes | covered | Keithley 2015 (user) |
| 2015_doc_2-servman.pdf | Keithley 2015 THD multimeter | yes | covered | Keithley 2015 service dup |
| 2213.pdf | Tektronix 2213 oscilloscope | no | n-a | Analog CRT scope |
| 2235_lg.pdf | Tektronix 2235 oscilloscope | no | n-a | Analog CRT scope |
| 2400S-900-01_K-Sep2011_User.pdf | Keithley 2400 SourceMeter | yes | covered | Keithley 2400 |
| 3245A OPM.pdf | HP 3245A Universal Source | yes | covered | 3245A migrated |
| 3245A-CLIP.pdf | HP 3245A Universal Source | yes | covered | 3245A |
| 3245A-CM.pdf | HP 3245A Universal Source | yes | covered | 3245A |
| 3245A-SM.pdf | HP 3245A Universal Source | yes | covered | 3245A |
| 33120A Service.pdf | HP 33120A function generator | yes | covered | 33120A migrated |
| 33120A User.pdf | HP 33120A function generator | yes | covered | 33120A migrated |
| 331A-332A._OSM.pdf | HP 331A/332A Distortion Analyzer | no | n-a | Analog distortion analyzer |
| 331A.pdf | HP 331A Distortion Analyzer | no | n-a | Dup |
| 3325B-IM.pdf | HP 3325B synthesizer/function gen | yes | covered | 3325B (pre-#70) |
| 3325B-OM.pdf | HP 3325B synthesizer/function gen | yes | covered | 3325B |
| 3325B-SM.pdf | HP 3325B synthesizer/function gen | yes | covered | 3325B |
| 3335A-OSM.pdf | HP 3335A Synthesizer/Level Generator | yes | covered | 3335A migrated |
| 3335A_OM.pdf | HP 3335A Synthesizer/Level Generator | yes | covered | 3335A migrated |
| 3400A-OSM-90008.pdf | HP 3400A RMS Voltmeter | no | n-a | Analog true-RMS voltmeter |
| 3400a .pdf | HP 3400A RMS Voltmeter | no | n-a | Dup |
| 3403C-OSM-1.pdf | HP 3403C True RMS Voltmeter | no | n-a | Analog RMS voltmeter (BCD only) |
| 3403c.pdf | HP 3403C True RMS Voltmeter | no | n-a | Dup |
| 34401A Service.pdf | HP 34401A DMM | yes | covered | 34401A (pre-#70) |
| 34401A User.pdf | HP 34401A DMM | yes | covered | 34401A |
| 3458A Front Panel Overview.pdf | HP 3458A DMM | yes | covered | 3458A (pre-#70) |
| 3458A Multimeter - Calibration Guide.pdf | HP 3458A DMM | yes | covered | 3458A |
| 3458A Multimeter User's Guide .pdf | HP 3458A DMM | yes | covered | 3458A |
| 3458A Quick reference guide.pdf | HP 3458A DMM | yes | covered | 3458A |
| 3499A User & Programming.pdf | HP 3499A switch/control system | yes | covered | 3499A (pre-#70) |
| 355C-D.pdf | HP 355C/355D step attenuator | no | n-a | Passive manual attenuator |
| 3580A-OSM.pdf | HP 3580A Audio Spectrum Analyzer | no | n-a | 1973 analog audio SA, no HP-IB |
| 3585-KS.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A migrated |
| 3585A User.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A/3585B migrated |
| 3585A-OM.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A |
| 3585A-SAT.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A |
| 3585A-SM-V1.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A |
| 3585A-SM-V2.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A |
| 3585A-SM-V3.pdf | HP 3585A Spectrum Analyzer | yes | covered | 3585A |
| 3585B-OM.pdf | HP 3585B Spectrum Analyzer | yes | covered | 3585B |
| 3585B-SM-V1.pdf | HP 3585B Spectrum Analyzer | yes | covered | 3585B |
| 3585B-SM-V2.pdf | HP 3585B Spectrum Analyzer | yes | covered | 3585B |
| 37204A.pdf | HP 37204A HP-IB Extender | no | n-a | Bus-extender accessory |
| 400E-EL-OSM.pdf | HP 400E/400EL AC Voltmeter | no | n-a | Analog AC voltmeter |
| 400EL.pdf | HP 400EL AC Voltmeter | no | n-a | Dup |
| 400F.pdf | HP 400F AC Voltmeter | no | n-a | Analog AC voltmeter |
| 400F_OSM.pdf | HP 400F AC Voltmeter | no | n-a | Dup |
| 4036B_manual.pdf | Symmetricom TSC 4036B RF Distribution Amp | no | n-a | RF distribution amp, no bus |
| 415E-OSM-90009.pdf | HP 415E SWR Meter | no | n-a | Analog SWR meter |
| 415e.pdf | HP 415E SWR Meter | no | n-a | Dup |
| 419A-OSM.pdf | HP 419A DC Null Voltmeter | no | n-a | Analog null voltmeter |
| 4275A-OM.pdf | HP 4275A Multi-Frequency LCR Meter | yes | covered | 4275A (pre-#70) |
| 4275A-SM-1.pdf | HP 4275A Multi-Frequency LCR Meter | yes | covered | 4275A |
| 428B-Manual.pdf | HP 428B Clip-on DC Milliammeter | no | n-a | Analog clip-on ammeter |
| 431C-OSM.pdf | HP 431C Power Meter | no | n-a | Analog thermistor power meter |
| 431C.pdf | HP 431C Power Meter | no | n-a | Dup |
| 432A-OSM.pdf | HP 432A Power Meter | no | n-a | Analog thermistor power meter |
| 432B-SN-3A.pdf | HP 432B Power Meter | no | n-a | Analog power meter DPM kit note |
| 432B-SUP.pdf | HP 432B Power Meter | no | n-a | Analog power meter supplement |
| 432C-OSM-SUP.pdf | HP 432C Power Meter | no | n-a | Analog thermistor power meter, no HP-IB |
| 432C-SN-3A.pdf | HP 432C Power Meter | no | n-a | Service note; analog 432C |
| 435A-OSM.pdf | HP 435A Power Meter | no | n-a | Analog power meter, no HP-IB |
| 435A-SAMPLE.pdf | HP 435A Power Meter | no | n-a | Analog 435A excerpt |
| 435B-OSM.pdf | HP 435B Power Meter | no | n-a | Analog power meter, no HP-IB |
| 435B.pdf | HP 435B Power Meter | no | n-a | Analog power meter |
| 436A.pdf | HP 436A Power Meter | yes | covered | 436A migrated |
| 437B-SM.pdf | HP 437B Power Meter | yes | covered | 437B migrated; service |
| 437B-UM.pdf | HP 437B Power Meter | yes | covered | 437B migrated; user |
| 437B.pdf | HP 437B Power Meter | yes | covered | 437B migrated |
| 438A-OSM.pdf | HP 438A Power Meter | yes | covered | 438A (pre-#70) |
| 438A.pdf | HP 438A Power Meter | yes | covered | 438A (pre-#70) |
| 44476B User.pdf | HP 3499A/B/C switch/control (mislabeled) | yes | covered | MISLABEL: content is 3499A/B/C; covered |
| 444A.pdf | Unidentified HP 4xx | unknown | unknown | Image-only scan, no text |
| 461A Mil Manal.pdf | HP 461A Amplifier | no | n-a | Analog amplifier (mil manual) |
| 461A Mil Manual.pdf | HP 461A Amplifier | no | n-a | Analog amplifier; duplicate |
| 461A-462A-OSM.pdf | HP 461A/462A Amplifier | no | n-a | Analog amplifiers, no bus |
| 461A.pdf | HP 461A Amplifier | no | n-a | Analog amplifier |
| 4725A.pdf | HP 4275A LCR Meter (mislabeled) | yes | covered | MISLABEL: content is 4275A; covered (pre-#70) |
| 478A-ON.pdf | HP 478A Thermistor Mount | no | n-a | Power sensor accessory |
| 4P008-00_03_E-paper Display COG Driver Interface Timing.pdf | E-paper COG driver | no | n-a | Display-driver datasheet |
| 4P008-00_03_E-paper+Display+COG+Driver+Interface+Timing.pdf | E-paper COG driver | no | n-a | Duplicate (URL-encoded) |
| 4P015-00_01_G2_Aurora Ma_COG Driver Interface Timing_for_small size_20150216.pdf | E-paper COG driver | no | n-a | Display-driver datasheet |
| 4P015-00_01_G2_Aurora+Ma_COG+Driver+Interface+Timing_for_small+size_20150216.pdf | E-paper COG driver | no | n-a | Duplicate (URL-encoded) |
| 5005A-OSM.pdf | HP 5005A Signature Multimeter | yes | covered | Variant of 5005B (migrated) |
| 5005B.pdf | HP 5005B Signature Multimeter | yes | covered | 5005B migrated |
| 5200A User Manual.pdf | Fluke 5200A AC Calibrator | no | n-a | N/A (non-VISA TTL RCU) |
| 5200A-OSM.pdf | Fluke 5200A AC Calibrator | no | n-a | N/A |
| 53131A CLIP.pdf | HP 53131A Universal Counter | yes | covered | 53131A (pre-#70) |
| 53131A Programming Guide.pdf | HP 53131A Universal Counter | yes | covered | 53131A |
| 53131A Service Guide.pdf | HP 53131A Universal Counter | yes | covered | 53131A |
| 53131A.pdf | HP 53131A Universal Counter | yes | covered | 53131A |
| 53310A-CLIP.pdf | HP 53310A Modulation Domain Analyzer | yes | covered | 53310A migrated |
| 53310A-OM.pdf | HP 53310A Modulation Domain Analyzer | yes | covered | 53310A |
| 53310A-PM.pdf | HP 53310A Modulation Domain Analyzer | yes | covered | 53310A |
| 53310A-SM.pdf | HP 53310A Modulation Domain Analyzer | yes | covered | 53310A |
| 53310AQuickStart.pdf | HP 53310A Modulation Domain Analyzer | yes | covered | 53310A |
| 5342A-OSM.pdf | HP 5342A Microwave Counter | yes | covered | 5342A (pre-#70) |
| 5342A.pdf | HP 5342A Microwave Counter | yes | covered | 5342A |
| 5343A-OSM.pdf | HP 5343A Microwave Counter | yes | covered | 5343A migrated |
| 5350A-51A-32A-OM.pdf | HP 5350A/5351A/5352A Counters | yes | covered | 5351A family |
| 5350A-51A-32A-SM.pdf | HP 5350A/5351A/5352A Counters | yes | covered | 5351A family |
| 5351A.pdf | HP 5351A Microwave Counter | yes | covered | 5351A (pre-#70) |
| 5440A-SM.pdf | Fluke 5440A DC Voltage Calibrator | yes | covered | 5440A backlogged |
| 5440B-AF Service Manual.pdf | Fluke 5440B/AF DC Voltage Calibrator | yes | covered | 5440 family (backlogged) |
| 5440B-AF User Manual.pdf | Fluke 5440B/AF DC Voltage Calibrator | yes | covered | 5440 family (backlogged) |
| 5440____smeng0100.pdf | Fluke 5440 DC Voltage Calibrator | yes | covered | 5440A backlogged |
| 54622 Programming.pdf | HP 54622A/D Oscilloscope | yes | covered | 54622 migrated |
| 54622 User Manual.pdf | HP 54622A/D Oscilloscope | yes | covered | 54622 migrated |
| 54845A Programmer.pdf | HP 54845A Infiniium | yes | covered | 54845A migrated |
| 54845A Quick Start.pdf | HP 54845A Infiniium | yes | covered | 54845A |
| 54845A Service.pdf | HP 54845A Infiniium | yes | covered | 54845A |
| 54845A user.pdf | HP 54845A Infiniium | yes | covered | 54845A |
| 59401A-OSM.pdf | HP 59401A Bus System Analyzer | no | n-a | HP-IB bus monitor, not a controllable instrument |
| 651B_MIL_MANUAL.pdf | HP 651B Test Oscillator | no | n-a | Analog signal source |
| 651B_OSM.pdf | HP 651B Test Oscillator | no | n-a | Analog oscillator |
| 6625A.pdf | HP 6625A System DC Power Supply | yes | covered | 6625A migrated |
| 7090A-OM.pdf | HP 7090A Measurement Plotting System | yes | covered | Already driven by HpPlotter (HpPlotterModel.Hp7090A); its analog-input digitize/recorder side is an optional feature gap |
| 7090A-SM.pdf | HP 7090A Measurement Plotting System | yes | covered | HpPlotter (service manual) |
| 720A-OSM.pdf | Fluke 720A Kelvin-Varley Divider | no | n-a | Passive manual divider |
| 720A____imeng0200_0.pdf | Fluke 720A Kelvin-Varley Divider | no | n-a | Passive divider; duplicate |
| 732A____imeng0000.pdf | Fluke 732A DC Reference Standard | no | n-a | Voltage reference, no bus |
| 7475A-OM.pdf | HP 7475A Plotter | yes | covered | Plotters backlogged (HP-GL) |
| 7475A-PM.pdf | HP 7475A Plotter | yes | covered | Plotters backlogged |
| 7475A-SM.pdf | HP 7475A Plotter | yes | covered | Plotters backlogged; service |
| 7550A-InterfacingAndProgrammingManual-07550-90001-483pages-Jan86.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged |
| 7550A-OperationAndInterconnectionManual-07550-90002-163pages-Oct84.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged |
| 7550A-ServiceManual-07550-90000-175pages-Jan86.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged; service |
| 7550A_OM.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged |
| 7550A_PM.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged |
| 7550A_SM.pdf | HP 7550A Plotter | yes | covered | Plotters backlogged; service |
| 778D.pdf | HP 778D Dual Directional Coupler | no | n-a | Passive microwave component |
| 809B-OSM.pdf | HP 809B Universal Probe Carriage (slotted-line accessory) | no | n-a | Passive RF fixture; Artek scan |
| 8116A-OSM-002.pdf | HP 8116A Pulse/Function Generator | yes | covered | 8116A migrated (#70); operating/service manual |
| 8116A-OSM-003.pdf | HP 8116A Pulse/Function Generator | yes | covered | 8116A migrated; service manual variant |
| 8116A-OSM-004.pdf | HP 8116A Pulse/Function Generator | yes | covered | 8116A migrated; service manual variant |
| 8116A-OSM-014.pdf | HP 8116A Pulse/Function Generator | yes | covered | 8116A migrated; service manual variant |
| 8340 Assembly Service.pdf | HP 8340A Synthesized Sweeper | yes | covered | Variant of 8340B (covered, epic #44); service manual |
| 8340 Calibration.pdf | HP 8340A Synthesized Sweeper | yes | covered | Variant of 8340B; calibration manual |
| 8340 Component Service.pdf | HP 8340A Synthesized Sweeper | yes | covered | Variant of 8340B; component-level service |
| 8340B Assembly.pdf | HP 8340B Synthesized Sweeper | yes | covered | 8340B migrated; assembly service |
| 8340B-8341B_CM.pdf | HP 8340B/8341B Synthesized Sweeper | yes | covered | 8340B migrated; combined 8340B/8341B service |
| 8340B-CM-V1.pdf | HP 8340B Synthesized Sweeper | yes | covered | 8340B migrated; component manual vol 1 |
| 8340B-CM-V2.pdf | HP 8340B Synthesized Sweeper | yes | covered | 8340B migrated; component manual vol 2 |
| 8340b User.pdf | HP 8340B Synthesized Sweeper | yes | covered | 8340B migrated; user manual |
| 8350A-9825_PN.pdf | HP 8350A Sweep Oscillator | yes | covered | Variant of 8350B (covered); HP 9825 programming note |
| 8350A-9835A_PN.pdf | HP 8350A Sweep Oscillator | yes | covered | Variant of 8350B; HP 9835A programming note |
| 8350A-9845B_PN.pdf | HP 8350A Sweep Oscillator | yes | covered | Variant of 8350B; HP 9845B programming note |
| 8350A-OSM.pdf | HP 8350A Sweep Oscillator | yes | covered | Variant of 8350B; operating/service manual |
| 8350B-430_AN.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated (#44); application/programming note |
| 8350B-HP85_PN.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated; HP 85 programming note |
| 8350B-LOM.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated; operating manual |
| 8350B-OSM.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated; operating/service manual |
| 8350B-QR.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated; quick reference |
| 8350b.pdf | HP 8350B Sweep Oscillator | yes | covered | 8350B migrated; manual |
| 83522A-OSM.pdf | HP 83522A RF Plug-In (for 8350) | no | n-a | Plug-in module; programmability is at 8350 mainframe |
| 83522A.pdf | HP 83522A RF Plug-In (for 8350) | no | n-a | Plug-in module; op/service manual (Agilent errata scan) |
| 83592-90074.pdf | HP 83592A RF Plug-In (for 8350) | no | n-a | Plug-in module; op/service manual |
| 83620A User.pdf | HP 83620A Synthesized Sweeper | yes | covered | 83620A migrated (#70); user manual |
| 83712B User.pdf | HP 83712B Synthesized CW Generator | yes | covered | 83712B migrated (#70); user manual |
| 8405A-OSM.pdf | HP 8405A Vector Voltmeter | no | n-a | Analog pre-GPIB VVM (distinct from covered 8508A) |
| 845A-OSM.pdf | Fluke 845A/845AB High-Impedance Voltmeter / Null Detector | no | n-a | Analog 1967 instrument; Artek scan |
| 845A_AB_imeng0000.pdf | Fluke 845A/845AB High-Impedance Voltmeter / Null Detector | no | n-a | Duplicate of 845A-OSM (instruction manual) |
| 8470B.pdf | HP 8470B Crystal Detector | no | n-a | Passive RF component |
| 8477A.pdf | HP 8477A Power Meter Calibrator | no | n-a | Manual/analog calibrator accessory |
| 8481A.pdf | HP 8481A Power Sensor | no | n-a | Passive power sensor (used with power meter) |
| 8481A_SM.pdf | HP 8481A Power Sensor | no | n-a | Passive sensor; service manual |
| 8481D_SM.pdf | HP 8481D Power Sensor | no | n-a | Passive sensor; service manual |
| 8484A.pdf | HP 8484A Power Sensor | no | n-a | Passive power sensor |
| 8491B.pdf | HP 8491B Fixed Attenuator | no | n-a | Passive attenuator |
| 8494-95-96G-H.pdf | HP/Keysight 8494/8495/8496 G/H Step Attenuators | no | n-a | Electromechanical; driven via 11713A (covered), no direct bus |
| 85082A.pdf | HP 85082A 50-Ohm Input Module | no | n-a | Passive accessory module (8505A network analyzer) |
| 8508A Operating and service.pdf | HP 8508A Vector Voltmeter | yes | covered | 8508A migrated (#70); operating/service manual |
| 8508A User Guide.pdf | HP 8508A Vector Voltmeter | yes | covered | 8508A migrated; user guide |
| 8560E Programming Guide.pdf | HP 8560E Spectrum Analyzer | yes | covered | 8560E backlogged (epic #44); programming guide |
| 8560E.pdf | HP 8560E Spectrum Analyzer | yes | covered | 8560E backlogged; manual |
| 85620A.pdf | HP 85620A Mass Memory Module | yes | covered | 85620A backlogged (epic #44) |
| 85671A phase noise utility.pdf | Agilent 85671A Phase Noise Utility | no | n-a | Software measurement personality, not an instrument |
| 85672 Spurious.pdf | Agilent 85672A Spurious Response Utility | no | n-a | Software measurement personality, not an instrument |
| 8591e Calibration Guide.pdf | HP 8591E Spectrum Analyzer | yes | covered | 8591E migrated (#70); calibration guide |
| 8591e Programming Guide.pdf | HP 8591E Spectrum Analyzer | yes | covered | 8591E migrated; programming guide |
| 8591e Service Guide.pdf | HP 8591E Spectrum Analyzer | yes | covered | 8591E migrated; service guide |
| 8591e.pdf | HP 8591E Spectrum Analyzer | yes | covered | 8591E migrated; manual |
| 8656A-OM.pdf | HP 8656A Signal Generator | yes | covered | 8656A migrated (#70); operating manual |
| 8656A-OSM.pdf | HP 8656A Signal Generator | yes | covered | 8656A migrated; operating/service manual |
| 8656B-OM.pdf | HP 8656B Signal Generator | yes | covered | 8656B migrated (#70); operating manual |
| 8656B-SM.pdf | HP 8656B Signal Generator | yes | covered | 8656B migrated; service manual |
| 8657B Service Vol 1.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated (#70); service vol 1 |
| 8657B Service Vol 2.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; service vol 2 |
| 8657B-OCM.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; operating/calibration manual |
| 8657B-QR.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; quick reference |
| 8657B-SM-V1.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; service manual vol 1 |
| 8657B-SM-V2.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; service manual vol 2 |
| 8657B.pdf | HP 8657B Signal Generator | yes | covered | 8657B migrated; manual |
| 8663 Service.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated (#70); service manual |
| 8663 User-Calibration.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; user/calibration manual |
| 8663A-SM-V1.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; service manual vol 1 |
| 8663A-SM-V2.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; service manual vol 2 |
| 8663A-SM-V3.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; service manual vol 3 |
| 8663A-SM-V4.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; service manual vol 4 |
| 8663A_OC.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; operating/calibration |
| 8663A_QR.pdf | HP 8663A Synthesized Signal Generator | yes | covered | 8663A migrated; quick reference |
| 8664A Repair.pdf | HP 8664A Synthesized Signal Generator | yes | covered | 8664A migrated (#70); repair manual |
| 8664A.pdf | HP 8664A Synthesized Signal Generator | yes | covered | 8664A migrated; manual |
| 8672A-OSM-90063.pdf | HP 8672A synthesized signal generator | yes | covered | 8672A migrated |
| 8672A.pdf | HP 8672A synthesized signal generator | yes | covered | 8672A migrated |
| 8673B Service.pdf | HP 8673B synthesized signal generator | yes | covered | 8673B (pre-#70) |
| 8673B User.pdf | HP 8673B synthesized signal generator | yes | covered | 8673B (pre-#70) |
| 8714 IBASIC.pdf | HP 8714 RF network analyzer | yes | covered | 8711-8714 migrated |
| 8714 Programmers Guide.pdf | HP 8714 RF network analyzer | yes | covered | 8711-8714 migrated |
| 8714 Service Guide.pdf | HP 8714 RF network analyzer | yes | covered | 8711-8714 migrated |
| 8714 User Guide.pdf | HP 8714 RF network analyzer | yes | covered | 8711-8714 migrated |
| 8714 tutorial.pdf | HP 8714 RF network analyzer | yes | covered | 8711-8714 migrated |
| 8714C LAN.pdf | HP 8714C RF network analyzer | yes | covered | 8714C |
| 8714ET_SRL.pdf | HP 8714ET RF network analyzer | yes | covered | 8714 family |
| 8720C Service Manual.pdf | HP 8720C microwave network analyzer | yes | covered | 8720C migrated |
| 8720C User Manual.pdf | HP 8720C microwave network analyzer | yes | covered | 8720C migrated |
| 8720C User.pdf | HP 8720C microwave network analyzer | yes | covered | 8720C migrated |
| 872XC_PG_REF.pdf | HP 8720C/8722C network analyzer | yes | covered | 8720C programming ref |
| 8757 User.pdf | HP 8757 scalar network analyzer | yes | covered | 8757D family |
| 8757D Operating-User.pdf | HP 8757D scalar network analyzer | yes | covered | 8757D migrated |
| 8757D Operating.pdf | HP 8757D scalar network analyzer | yes | covered | 8757D migrated |
| 8757D Service.pdf | HP 8757D scalar network analyzer | yes | covered | 8757D migrated |
| 8901A-OM.pdf | HP 8901A modulation analyzer | yes | covered | 8901A/B migrated |
| 8901A-SM.pdf | HP 8901A modulation analyzer | yes | covered | 8901A/B migrated |
| 8901A-SRV-NOTES.pdf | HP 8901A modulation analyzer | yes | covered | 8901A/B migrated |
| 8901A_AN-286-1a.pdf | App note (8901A) | no | n-a | Application note |
| 8901A_MIL_CAL.pdf | HP 8901A modulation analyzer | yes | covered | 8901A/B migrated |
| 8901A_MIL_MANUAL.pdf | HP 8901A modulation analyzer | yes | covered | 8901A/B migrated |
| 8901B Service Manual - Vol 2.pdf | HP 8901B modulation analyzer | yes | covered | 8901A/B migrated |
| 8901B Service Manual - Vol 3.pdf | HP 8901B modulation analyzer | yes | covered | 8901A/B migrated |
| 8901B Service Manual.pdf | HP 8901B modulation analyzer | yes | covered | 8901A/B migrated |
| 8901B User.pdf | HP 8901B modulation analyzer | yes | covered | 8901A/B migrated |
| 8902 Service Manual - 2.pdf | HP 8902 measuring receiver | yes | covered | 8902A family |
| 8902 Service Manual Searchable.pdf | HP 8902 measuring receiver | yes | covered | 8902A family |
| 8902 Service Manual.pdf | HP 8902 measuring receiver | yes | covered | 8902A family |
| 8902A Basic Guide.pdf | HP 8902A measuring receiver | yes | covered | 8902A (pre-#70) |
| 8902A Full Service Manual - New.pdf | HP 8902A measuring receiver | yes | covered | 8902A |
| 8902A Full Service Manual.pdf | HP 8902A measuring receiver | yes | covered | 8902A |
| 8902A Microwave Product Note.pdf | Product note (8902A) | no | n-a | Product note |
| 8902A Operation & Calibration.pdf | HP 8902A measuring receiver | yes | covered | 8902A |
| 8902A Service Manual Full - Searchable.pdf | HP 8902A measuring receiver | yes | covered | 8902A |
| 8902A.pdf | HP 8902A measuring receiver | yes | covered | 8902A |
| 8903B Service Manual 2.pdf | HP 8903B audio analyzer | yes | covered | 8903B migrated |
| 8903B Service Manual.pdf | HP 8903B audio analyzer | yes | covered | 8903B migrated |
| 8903b.pdf | HP 8903B audio analyzer | yes | covered | 8903B migrated |
| 8970B-OM.pdf | HP 8970B noise figure meter | yes | covered | 8970B migrated |
| 8970B-SM.pdf | HP 8970B noise figure meter | yes | covered | 8970B migrated |
| 9121D-S_9122D-S_OperatorsManual_09122-90000_83pages_Jun84.pdf | HP 9121D/9122D disc drive | no | n-a | HP-IB mass-storage peripheral |
| 9122D-S_ServiceManual_5957-6559_63pages_Apr88.pdf | HP 9122D disc drive | no | n-a | HP-IB storage peripheral |
| A21-R107 API 100 kHz Spur Adjustment.pdf | 3325B adjustment procedure | no | n-a | Manual adjustment procedure |
| AC Voltage Measurement Errors in Digital Multimeters (AN 1389-3).pdf | App note AN 1389-3 | no | n-a | App note |
| AMC1100 Isolation Amplifier.pdf | TI AMC1100 | no | n-a | Component datasheet |
| APP_NOTE_171-1.pdf | App note 171-1 | no | n-a | App note |
| APP_NOTE_77-1.pdf | App note 77-1 | no | n-a | App note |
| APP_NOTE_91.pdf | App note 91 | no | n-a | App note |
| ARRL SWR.pdf | ARRL SWR article | no | n-a | Reference article |
| Agilent_HP_Basic_for_Windows_Operator_Manual-E2060-90001.pdf | HP BASIC for Windows | no | n-a | Software manual |
| Agilent_HP_Basic_for_Windows_Reference_Manual-E2060-90002.pdf | HP BASIC for Windows | no | n-a | Software manual |
| Appnote-4-Power-tests1.pdf | App note (power tests) | no | n-a | App note |
| BASIC6.2_CompilingPrograms_98618-90001_184pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_DevelopingCSUBs_E2040-90003_200pages_Aug91 (1).pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_DevelopingCSUBs_E2040-90003_200pages_Aug91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_InterfaceReference_98616-90013_606pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_LanguageReferenceA-N_98616-90004_572pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_LanguageReferenceVol2_O-Z_98616-90004_657pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_PortingAndGlobalization_98616-90014_227pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC6.2_ProgrammingGuide_98616-90010_626pages_Jun91.pdf | HP BASIC 6.2 | no | n-a | Software manual |
| BASIC_AdvancedProgrammingTechniques_98616-90204_306pages_Jan94.pdf | HP BASIC | no | n-a | Software manual |
| BP791IT_IM.pdf | Omron BP791IT blood pressure monitor | no | n-a | Consumer medical device |
| Basic THD Measurement.pdf | App note (THD) | no | n-a | App note |
| BugOutLantern.pdf | Camping lantern | no | n-a | Consumer product |
| C3-4k Prog Man V5_2.pdf | Inner Range security panel | no | n-a | Access-control panel, not GPIB |
| CONTENTS.pdf | Generic contents page | no | n-a | Not an instrument |
| CR123A Li-Ion Battery Specs.pdf | CR123A battery | no | n-a | Component spec |
| Calculating Measurement Uncertainty ... Ratio Measurement Techniques.pdf | App note | no | n-a | App note |
| Calibration of Precision Step Attenuators.pdf | App note (step attenuator cal) | no | n-a | Application note |
| Component Level Service Guide.pdf | Unidentified service guide | no | n-a | Scanned, no text |
| DE-5000_manu_en.pdf | DER EE DE-5000 handheld LCR meter | no | n-a | Handheld; proprietary IR-USB logging only |
| DG1000Z Programming Guide.pdf | Rigol DG1000Z AWG | yes | covered | DG1000Z migrated |
| DG1000Z User's Guide.pdf | Rigol DG1000Z AWG | yes | covered | DG1000Z |
| DG1000Z%20User's%20Guide.pdf | Rigol DG1000Z AWG | yes | covered | DG1000Z (URL-encoded dup) |
| DM3058 Calibration Guide.pdf | Rigol DM3058 DMM | yes | covered | DM3058 (pre-#70) |
| DM3058-User-Guide.pdf | Rigol DM3058 DMM | yes | covered | DM3058 |
| DM3058_Programming Guide.pdf | Rigol DM3058 DMM | yes | covered | DM3058 |
| DMC-100_User_Guide.pdf | DMC-100 clamp DMM | no | n-a | Handheld clamp meter, no bus |
| DP800Programming.pdf | Rigol DP800 PSU | yes | covered | DP832 family |
| DP832.pdf | Rigol DP832 PSU | yes | covered | DP832 (pre-#70) |
| DPO3034User.pdf | Tektronix DPO3034 scope | yes | covered | DPO3000/MSO3000 |
| DPO4034 performance.pdf | Tektronix DPO4034 scope | yes | covered | DPO4000/MSO4000 |
| DPO4034 service.pdf | Tektronix DPO4034 scope | yes | covered | DPO4000/MSO4000 |
| DPO4034.pdf | Tektronix DPO4034 scope | yes | covered | DPO4000/MSO4000 |
| DS1000Z_Programming Guide_EN.pdf | Rigol DS1000Z scope | yes | covered | DS1054Z family |
| DSA800_UserGuide.pdf | Rigol DSA800 spectrum analyzer | yes | covered | DSA800 |
| Datron_4708_Autocal_..._Operator_Manual.pdf | Datron 4708 multifunction calibrator | yes | n-a | Dropped - not owned |
| Datron_4708_Autocal_..._Service_Manual.pdf | Datron 4708 calibrator | yes | n-a | Dropped - not owned |
| E2060-90001 hp basic[173].pdf | HP BASIC language manual | no | n-a | Software/language doc |
| E2083-90000_InstrumentBASIC_UsersHandbook...pdf | HP Instrument BASIC handbook | no | n-a | Software/language doc |
| E3633A Service Guide.pdf | Agilent E3633A DC power supply | yes | covered | E3633A (pre-#70) |
| E3633A User Guide.pdf | Agilent E3633A DC power supply | yes | covered | E3633A |
| E4406A Programmers Guide.pdf | Agilent E4406A VSA | yes | covered | E4406A backlogged |
| E4406A Service.pdf | Agilent E4406A VSA | yes | covered | E4406A |
| E4406A Test.pdf | Agilent E4406A VSA | yes | covered | E4406A |
| E4406A_Manual.pdf | Agilent E4406A VSA | yes | covered | E4406A |
| E4418-90064.pdf | Agilent E4418 power meter | yes | covered | E4418B (pre-#70) |
| E4418-90065.pdf | Agilent E4418 power meter | yes | covered | E4418B |
| E4418-90066.pdf | Agilent E4418 power meter | yes | covered | E4418B |
| E4418B CLIP V02.pdf | Agilent E4418B power meter | yes | covered | E4418B |
| E4418B Programming.pdf | Agilent E4418B power meter | yes | covered | E4418B |
| E4418B Service.pdf | Agilent E4418B power meter | yes | covered | E4418B |
| E4418B-E23 LOIF V01 _2_.pdf | Agilent E4418B power meter | yes | covered | E4418B |
| E4418B.pdf | Agilent E4418B power meter | yes | covered | E4418B |
| E4436B Calibration.pdf | Agilent E4436B signal generator | yes | covered | E4436B migrated |
| E4436B Quick Start.pdf | Agilent E4436B signal generator | yes | covered | E4436B |
| E4436B UN8.pdf | Agilent E4436B signal generator | yes | covered | E4436B |
| E4436B User.pdf | Agilent E4436B signal generator | yes | covered | E4436B |
| E4438C Calibration.pdf | Agilent E4438C ESG sig gen | yes | covered | E4438C backlogged |
| E4438C Error Messages.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C Key and Data Reference 1.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C Key and Data Reference 2.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C SCPI Command Reference.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C SCPI Reference 1.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C SCPI Reference 2.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C SCPI Reference 3.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E4438C User Guide.pdf | Agilent E4438C sig gen | yes | covered | E4438C |
| E5810A User Guide.pdf | Agilent E5810A LAN/GPIB gateway | yes | n-a | Bus gateway infrastructure, not an instrument |
| FLIR-t810375-en-us.pdf | FLIR T810 thermal camera | no | n-a | Thermal camera, no bus |
| FX888D.pdf | Hakko FX-888D soldering station | no | n-a | Soldering station |
| Fluke - Calibration Philosophy in practice.pdf | Fluke app note | no | n-a | App note |
| Fluke 179 User.pdf | Fluke 179 handheld DMM | no | n-a | Handheld, no remote interface |
| Fluke 5200A Student Handout.pdf | Fluke 5200A AC calibrator | no | n-a | N/A (non-VISA TTL RCU) |
| Fluke_732A_752A_Specifications.pdf | Fluke 732A/752A reference/divider | no | n-a | DC standard + passive divider |
| Fridge.pdf | Refrigerator | no | n-a | Household appliance |
| GERange.pdf | GE electric range/oven | no | n-a | Household appliance |
| GPIB-120A.pdf | NI GPIB-120A bus extender | yes | n-a | GPIB infrastructure, not an instrument |
| GPIB-232CT-A-GettingStarted.pdf | NI GPIB-232CT-A converter | yes | n-a | GPIB infrastructure, not an instrument |
| General_Electric_Glow_Lamp_Datasheet_1966.pdf | GE glow lamp | no | n-a | Component datasheet |
| GuardTerminal.pdf | Guard terminal app note | no | n-a | Measurement-technique note |
| GuardTerminal_EPS_Feb2011.pdf | Guard terminal app note | no | n-a | Duplicate |
| HP 11792A Operating & Service.pdf | HP 11792A sensor module | no | n-a | Accessory sensor module (8902A) |
| HP 436A Service.pdf | HP 436A power meter | yes | covered | 436A migrated |
| HP 44476A, B Operating, Programming, & Configuration.pdf | HP 44476A/B switch modules | yes | n-a | Plug-in switch modules for a mainframe; accessory |
| HP 8340A Operating & Service Vol. 3.pdf | HP 8340A synthesized sweeper | yes | covered | 8340B family |
| HP 8340B, 41B Assembly Level Service.pdf | HP 8340B/8341B sweeper | yes | covered | 8340B |
| HP 8340B, 41B Operating Information.pdf | HP 8340B/8341B sweeper | yes | covered | 8340B |
| HP 8340B, 41B Operating.pdf | HP 8340B/8341B sweeper | yes | covered | 8340B |
| HP 8481A-8482A-8483A-RCHD.pdf | HP 8481A/8482A/8483A power sensors | no | n-a | Accessory power sensors |
| HP 8757D Operating.pdf | HP 8757D scalar network analyzer | yes | covered | 8757D migrated |
| HP Jounral Apr 89 - 3458A.pdf | HP 3458A DMM (journal article) | no | n-a | Journal article; 3458A covered |
| HP SYS-II Cabinets.pdf | HP System II rack cabinets | no | n-a | Rack/enclosure accessory |
| HP-415B-Manual.pdf | HP 415B SWR Meter | no | n-a | Analog meter, no bus |
| HP-419 - Fluke 845 Modification ... 2015-06-04.pdf | HP 419 DC null detector (mod) | no | n-a | Hand-drawn mod schematics |
| HP-419A Marked-up Schematic.pdf | HP 419A DC null voltmeter | no | n-a | Schematic only |
| HP-651B-Manual-sn-647.pdf | HP 651B Test Oscillator | no | n-a | Analog oscillator |
| HP419Mod.pdf | HP 419 null detector (mod) | no | n-a | Modification doc |
| HP8405A PROBE REPAIR.pdf | HP 8405A Vector Voltmeter | no | n-a | Probe repair service note |
| HPIB_tutorial_HP[342].pdf | HP-IB bus tutorial | no | n-a | Tutorial |
| HPJ-1955-03.pdf | HP Journal 1955 | no | n-a | Journal |
| HP_10514_Mixer_Jan_1967.pdf | HP 10514A Mixer | no | n-a | Passive component |
| HP_3580A_Operating_and_Service_Manual.pdf | HP 3580A Audio Spectrum Analyzer | no | n-a | No HP-IB |
| HP_415E_SWR_Meter_Operation_Service_Manual.pdf | HP 415E SWR Meter | no | n-a | Analog meter |
| HP_419A_op_service_manual_1966_HQ.pdf | HP 419A DC null voltmeter | no | n-a | Analog |
| HP_437B_Service_Manual.pdf | HP 437B Power Meter | yes | covered | 437B migrated |
| HP_8656B_Service_Manual_volume_2_and_3.pdf | HP 8656B Signal Generator | yes | covered | 8656 migrated |
| HP_8672A_..._Service_Manual_08672-90058_March_78.pdf | HP 8672A Signal Generator | yes | covered | 8672A migrated |
| HP__8481B_OM_SM_1.pdf | HP 8481B Power Sensor | no | n-a | Sensor accessory |
| HP__8481H_OM_SM_1.pdf | HP 8481H Power Sensor | no | n-a | Sensor accessory |
| HTBasic-Help.pdf | HTBasic software help | no | n-a | Software |
| How Cables and Connectors Impact Measurement Uncertainty ... .pdf | App note | no | n-a | App note |
| Improved AC Measurements Using Digital Multimeters - Application Note.pdf | App note | no | n-a | App note |
| InstallingAndUsing_HPBASIC_InMS-DOSEnvironment_82324-90000_326pages_Nov90.pdf | HP BASIC for MS-DOS | no | n-a | Software |
| InstallingAndUsing_HPBASIC_InMS-DOSEnvironment_82324-90000_326pages_Nov90 (dup).pdf | HP BASIC for MS-DOS | no | n-a | Software (dup listing) |
| InstallingAndUsing_HPINstrumentBASIC-Windows_E2200-90000_209pages_Feb92.pdf | HP Instrument BASIC (Windows) | no | n-a | Software |
| InstantPot-IP-DUO-Manual-English.pdf | Instant Pot | no | n-a | Consumer appliance |
| K2000-schemaric-reverse-enged.pdf | Keithley 2000 DMM | yes | NEW | GPIB DMM; not in covered (only 2015/2400) |
| K20UCG500012714.pdf | Keurig coffee brewer | no | n-a | Consumer appliance |
| KEI 2015 Service.pdf | Keithley 2015 DMM | yes | covered | Keithley 2015 |
| Keithley_Model_2000_Multimeter_Repair_Manual.pdf | Keithley 2000 DMM | yes | NEW | Programmable GPIB DMM |
| LC574AL.pdf | LeCroy LC574AL Oscilloscope | yes | covered | LC574A |
| LT1634 - Micropower Precision Shunt Voltage Reference.pdf | Linear Tech LT1634 | no | n-a | Component datasheet |
| LT1964 - 200mA Low Noise Negative LDO.pdf | Linear Tech LT1964 | no | n-a | Component datasheet |
| LT3060 - 45V VIN Micropower LDO.pdf | Linear Tech LT3060 | no | n-a | Component datasheet |
| LTC2054-2055 Zero Drift Op Amp.pdf | Linear Tech LTC2054/2055 | no | n-a | Component datasheet |
| LTC6255 Low current RR Op Amp.pdf | Linear Tech LTC6255 | no | n-a | Component datasheet |
| LeCroy-5674 User.pdf | LeCroy LC-Series Oscilloscope | yes | covered | LC574A family |
| LeCroy-5674 service.pdf | LeCroy LC-Series Oscilloscope | yes | covered | LC574A family |
| LeCroy-5674.pdf | LeCroy LC-Series Oscilloscope | yes | covered | LC574A family |
| M404_QSG-TG-OldToshiba.pdf | M404 ADSL modem | no | n-a | Networking device |
| M404_QSG.pdf | M404 ADSL modem | no | n-a | Networking device |
| M404_User_Manual-TG-OldToshiba.pdf | M404 ADSL modem | no | n-a | Networking device |
| M404_User_Manual.pdf | M404 ADSL modem | no | n-a | Networking device |
| M9811.pdf | Maynuo M9811 Programmable DC Electronic Load | yes | NEW | M97xx-series programmable load |
| MSO1000Z&DS1000Z_UserGuide.pdf | Rigol MSO1000Z/DS1000Z scope | yes | covered | DS1054Z family |
| MSO3000-and-DPO3000-Programmer-Manual.pdf | Tektronix MSO3000/DPO3000 | yes | covered | DPO3000/MSO3000 |
| Make Better RMS Measurements with Your DMM - Application Note.pdf | App note | no | n-a | App note |
| Manual_6502B.pdf | 6502B RF Distribution unit | no | n-a | RF distribution amp, no bus |
| Maximizing Your Reference Multimeter ... .pdf | App note | no | n-a | App note |
| Migrating from DC Voltage Dividers ... .pdf | App note | no | n-a | App note |
| MotorolaWT-TG-OldToshiba.pdf | Motorola walkie-talkie | no | n-a | Consumer radio |
| N9320A User Guide.pdf | Agilent N9320A Spectrum Analyzer | yes | covered | N9320A migrated |
| N9320A_service_guide.pdf | Agilent N9320A Spectrum Analyzer | yes | covered | N9320A |
| NI-VISA User Manual.pdf | NI-VISA software | no | n-a | Software |
| Network Analyzer Measurements Filter and Ampliier Examples.pdf | App note | no | n-a | App note |
| Noise investigation ... HP3457A or HP3458A ... .pdf | Technical paper (3457A/3458A) | no | n-a | Paper; 3458A covered |
| Pages from HP 8340B, 41B Operating Manual.pdf | HP 8340B sweeper | yes | covered | 8340B |
| Pattern_names_help.pdf | Software help | no | n-a | Software |
| Precision in Practice ... Digital Multimeter measurements.pdf | App note | no | n-a | App note |
| Programming Guide_DM3058.pdf | Rigol DM3058 DMM | yes | covered | DM3058 |
| Quick Start Guide.pdf | Unidentified | unknown | unknown | Scanned, no text |
| README.pdf | Unidentified | unknown | unknown | No extractable text |
| RF4287ha.pdf | Samsung refrigerator | no | n-a | Consumer appliance (NOT Agilent 4287A) |
| RFPower_Meas AN64-1B.pdf | App note AN64-1B | no | n-a | App note |
| RFViewer.pdf | Software | no | n-a | Software |
| RGPK Spec Sheet.pdf | Rheem furnace | no | n-a | HVAC |
| RGPK Use and Care Instructions.pdf | Rheem furnace | no | n-a | HVAC |
| RX-V673_manual-TG-OldToshiba.pdf | Yamaha RX-V673 AV receiver | no | n-a | Consumer AV |
| RX-V673_manual.pdf | Yamaha RX-V673 AV receiver | no | n-a | Consumer AV |
| Resistance; DC Current; AC Current; ... Errors in DMMs.pdf | App note | no | n-a | App note |
| Rohde_Schwarz_SMT(SME)_with_DMC01_AN.pdf | R&S SMT/SME Signal Generator | yes | covered | R&S SME/SMT migrated |
| SA Basics - App Note 150.pdf | HP App Note 150 | no | n-a | App note |
| SENCORE PR57 Operation and Service.pdf | Sencore PR57 Powerite AC analyzer | no | n-a | Bench AC line analyzer, no bus |
| SS17.pdf | HP 8902A measuring receiver (Service Sheet 17) | yes | covered | 8902A migrated; fragment |
| SamsungCRG9.pdf | Samsung CRG9 monitor | no | n-a | Consumer display |
| Service Manual Vol2 08902-99002.pdf | HP 8902A measuring receiver | yes | covered | 8902A migrated (service) |
| Service Manual Vol3 08902-90024.pdf | HP 8902A measuring receiver | yes | covered | 8902A migrated (service) |
| SpecAn Amp Accuracy.pdf | Spectrum analyzer app note | no | n-a | App note |
| SpecAn Basics.pdf | Spectrum analyzer app note | no | n-a | App note |
| SpecAn Fundamentals.pdf | Spectrum analysis app note | no | n-a | App note |
| SunKettle.pdf | Consumer kettle | no | n-a | Consumer appliance |
| Symmetricom GPS Receiver Clock 149.pdf | Symmetricom GPSDO 10MHz clock | yes | unknown | RS-232 help-shell; hobby-grade GPSDO module, borderline scope |
| System Cabling Errors (AN 1389-1).pdf | Keysight AN 1389-1 | no | n-a | App note |
| System_Overview.pdf | HP 8405A vector voltmeter (tech data) | no | n-a | Vintage analog VVM, no bus |
| TDS 784D User.pdf | Tektronix TDS784D scope | yes | covered | TDS784 migrated |
| TDS784C Programmer.pdf | Tektronix TDS784C scope | yes | covered | TDS784 migrated |
| TDS784C Service.pdf | Tektronix TDS784C scope | yes | covered | TDS784 migrated (service) |
| TDS784C User.pdf | Tektronix TDS784C scope | yes | covered | TDS784 migrated |
| TDS784D Service Manual.pdf | Tektronix TDS784D scope | yes | covered | TDS784 migrated (service) |
| TDS784D Service.pdf | Tektronix TDS784D scope | yes | covered | Dup service manual |
| TDS784D Technical Reference Manual.pdf | Tektronix TDS784D scope | yes | covered | TDS784 migrated |
| TDS784D User Reference.pdf | Tektronix TDS784D scope | yes | covered | TDS784 migrated |
| TDS784D User.pdf | Tektronix TDS784D scope | yes | covered | TDS784 migrated |
| TUR.pdf | Test Uncertainty Ratio doc | no | n-a | Image-only; metrology note |
| Tek_2465_Operators_and_Instruction_Manual.pdf | Tektronix 2465 analog scope | no | n-a | Analog CRT scope, no bus |
| Tek_2465_Service_and_Instruction_Manual_Oct84.pdf | Tektronix 2465 analog scope | no | n-a | Analog scope service manual |
| The Fundamentals of Signal Analysis.pdf | HP signal analysis note | no | n-a | App note |
| ThinkJetPrinter_OperatorsManual.pdf | HP ThinkJet printer | no | n-a | Printer (HP-IB output device) |
| Troubleshooting_Analog_Circuits-TG-INTEL.pdf | Pease analog-circuits book | no | n-a | Reference text |
| Troubleshooting_Analog_Circuits.pdf | Pease analog-circuits book | no | n-a | Reference text (dup) |
| U1253BUser.pdf | Keysight U1253B handheld DMM | yes | NEW | Remote command set over proprietary IR-USB serial; handheld, borderline (not GPIB/VISA) |
| Understanding and Comparing Instrument Specifications.PDF | App note | no | n-a | App note |
| Understanding and Improving Network Analyzer Dynamic Range.pdf | App note | no | n-a | App note |
| Understanding the Fundamental Principles of Vector Network Analysis.pdf | App note | no | n-a | App note |
| UserGuide_AVioHD.pdf | AVio HD (AV device) | no | n-a | Consumer AV |
| UsingTheHP-IBInterfaceAndCommandLibraryWithDOS.pdf | HP-IB command library for DOS | no | n-a | Software/programming guide |
| VEE6AdvancedTopics.pdf | HP VEE software | no | n-a | Software manual |
| VEE_Practical_Graphical_Programming.pdf | HP VEE software | no | n-a | Software book |
| VevorMiterGuage04302026.pdf | Vevor miter gauge | no | n-a | Shop tool |
| WaveRunner 6000 - Getting Started.pdf | LeCroy WaveRunner 6000 scope | yes | covered | WaveRunner 6000 migrated |
| ag06_en_om_a0.pdf | Yamaha AG06 mixing console | no | n-a | Consumer audio mixer |
| an_170-1.pdf | HP App Note 170-1 | no | n-a | App note |
| an_183.pdf | HP App Note 183 | no | n-a | App note |
| an_218-5.pdf | HP App Note 218-5 | no | n-a | App note |
| an_60.pdf | HP App Note 60 | no | n-a | App note |
| an_69.pdf | HP App Note 69 | no | n-a | App note |
| er-gb40_mul_om.pdf | Panasonic ER-GB40 groomer | no | n-a | Consumer appliance |
| fluke_845a_ab_sm.pdf | Fluke 845A/845AB null detector | no | n-a | Analog null detector, no bus |
| hfr60-62-600-im-n-en.pdf | Canon Vixia camcorder | no | n-a | Consumer camcorder |
| hp310A_v6.pdf | HP 310A (image-only scan) | unknown | unknown | Presumed vintage analog wave analyzer/selective voltmeter, no bus |
| hp_xref-free.pdf | HP semiconductor cross-reference | no | n-a | Reference guide |
| hpbasic_plus_nb.pdf | HP BASIC software notebook | no | n-a | Software manual |
| kei2015-man.pdf | Keithley 2015 THD multimeter | yes | covered | Keithley 2015 migrated |
| kei2015-sman.pdf | Keithley 2015 THD multimeter | yes | covered | Keithley 2015 (service) |
| keit2015-data.pdf | Keithley 2015 datasheet | yes | covered | Keithley 2015 (datasheet) |
| krohn_hite_3202r_sm.pdf | Krohn-Hite 3202R filter | no | n-a | Analog tunable filter |
| kx-tg6582_en_om.pdf | Panasonic cordless phone | no | n-a | Consumer phone |
| megohmmeter-insulation-resistance-testing.pdf | Insulation-resistance note | no | n-a | App note |
| oven.pdf | Consumer oven | no | n-a | Consumer appliance |
| phs925st1ss.pdf | GE PHS925 range/oven | no | n-a | Consumer appliance |
| resmed-airsense-10-clinical-guide.pdf | ResMed AirSense 10 CPAP | no | n-a | Medical consumer device |
| rf4287hars.pdf | Samsung RF4287 refrigerator | no | n-a | Consumer appliance |
| smd-00243_AN30.pdf | App Note AN30 | no | n-a | App note |
| sme_14e.pdf | R&S SME02/03/06 signal generator | yes | covered | R&S SME migrated |
| tektronix_2213_service.pdf | Tektronix 2213 analog scope | no | n-a | Analog scope service manual |
| tektronix_an-usm-488-service_manual.pdf | Tektronix AN/USM-488 (mil 2465) scope | no | n-a | Analog scope, no bus |
| tn1297s.pdf | NIST TN 1297 | no | n-a | Metrology guide |
| vixiahfr70-72-700-im-en.pdf | Canon Vixia camcorder | no | n-a | Consumer camcorder |
