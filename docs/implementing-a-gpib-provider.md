# Implementing a GPIB provider

All instrument I/O in GPIBUtils-NG goes through one small, vendor-neutral abstraction in
`GpibUtils.Visa`. Instrument drivers only ever see **`IInstrumentSession`**; how those bytes reach the
instrument is decided by an **`IGpibProvider`**. That means you can add support for a new controller —
[Keysight VISA](https://www.keysight.com/), a [Prologix](https://prologix.biz/) GPIB-USB/-Ethernet, an
[AR488](https://github.com/Twilight-Logic/AR488) Arduino, a raw board driver — **without touching a
single instrument driver**.

```
Instrument drivers  ->  IInstrumentSession        (what a driver programs against)
                            ^
                            |  created by
                            |
GpibProviders (registry) -> IGpibProvider          NI-VISA (default), NI-488.2,
                                                    Keysight-VISA / Prologix / AR488 (stubs),
                                                    Simulated, and *your* provider
```

## The two interfaces

### `IGpibProvider`
A pluggable backend. It names itself, advertises what it can do, reports whether it can run on this
machine, discovers resources, and opens sessions.

| Member | Contract |
|---|---|
| `Name` | Stable, human-readable selector, e.g. `"Prologix"`. |
| `Capabilities` | Honest `ProviderCapabilities` — discovery, serial poll, SRQ, device clear, return-to-local, native addressing. |
| `IsAvailable` / `UnavailableReason` | False + a reason when the driver/hardware/assembly is missing, so callers degrade or refuse cleanly instead of throwing blindly. |
| `Discover(filter)` | Enumerate resources (empty list if unsupported — never throw for "none found"). |
| `Open(resource, settings)` | Return a live `IInstrumentSession`. |
| `DescribeError(ex)` | Decode a backend exception to a `GpibStatus` (name/meaning/code), or `GpibStatus.Empty`. |

Implement the optional **`INativeGpib`** on the same class only if you support board/primary/secondary
addressing (NI-488.2 style).

### `IInstrumentSession`
A live connection to one instrument. Implement the byte movers and the GPIB control operations:

- `Write(string)` / `WriteBytes(byte[], assertEnd)` — send a command; honour `WriteTermination` and
  `AssertEndOnWrite` (EOI on the last byte; pass `assertEnd: false` for intermediate chunks of a
  streamed message so a mid-message EOI doesn't fragment it).
- `ReadString()` / `ReadBytes(maxBytes)` — read a reply; `maxBytes == 0` means read to
  `ReadTermination` / EOI.
- `Query(string)` — convenience: `Write` then `ReadString`.
- `SerialPoll()` — return the `StatusByte` (RQS/MAV/ESB are decoded for you).
- `WaitForServiceRequest(timeoutMs, out elapsedMs)` — true if SRQ asserted before the timeout.
- `Clear()` — IEEE 488.2 device clear.
- `ReturnToLocal()` — hand the front panel back (no-op if unsupported).

Sessions need not be thread-safe; a driver serializes access to its own session.

## Steps to add a provider

1. **Create the class** in `src/GpibUtils.Visa/Providers/` (or your own project that references
   `GpibUtils.Visa`). If your adapter needs a serial port or socket, add that dependency there — keep it
   out of `GpibUtils.Visa`'s core so the default build stays dependency-light.
2. **Implement `IGpibProvider`** and a matching `IInstrumentSession`. Use the
   [`SimulatedGpibProvider`](../src/GpibUtils.Visa/Simulation/SimulatedGpibProvider.cs) as a minimal,
   dependency-free reference and the [`NiVisaGpibProvider`](../src/GpibUtils.Visa.Ni/NiVisaGpibProvider.cs)
   as the real-hardware reference.
3. **Advertise honest `Capabilities`.** If you can't do SRQ or discovery, say so.
4. **Register it.** Either call `GpibProviders.Register(new MyProvider())` at startup, or — for a
   built-in — add it to `RegisterBuiltIns()` in
   [`GpibProviders`](../src/GpibUtils.Visa/GpibProviders.cs).
5. **Select it.** It's now resolvable by name: `GpibProviders.Get("MyProvider")`,
   `GpibProviders.Open("MyProvider", resource, settings)`, or make it the default via
   `GpibProviders.DefaultProviderName = "MyProvider"` or the `GPIBUTILS_GPIB_PROVIDER` environment
   variable.
6. **Test it** against the same suite shape as `tests/GpibUtils.Visa.Tests` (the simulator tests are a
   good template — no hardware required).

## Addressing model

The canonical identifier is a VISA-style resource string, e.g. `GPIB0::18::INSTR`. A VISA-based provider
passes it straight through. A serial controller (Prologix/AR488) addresses by **primary GPIB address**
over a `++` command set:

```
++mode 1        # controller mode
++addr 18       # talk/listen to GPIB primary address 18
++eoi 1         # assert EOI on the last byte
++read eoi      # read the instrument's reply
++spoll 18      # serial poll -> status byte
++srq           # 1 if SRQ asserted
```

Parse the primary address out of the resource string and get the COM port / IP from your provider's
configuration. A convenient convention is `PROLOGIX::COM5::9` or `AR488::COM7::9` — the provider splits
on `::`, opens the port, and talks to primary address `9`.

## Conventions

- **Encoding:** strings map to bytes as Latin-1 (ISO-8859-1, 1:1) so binary blocks survive a
  round-trip. This is a **requirement** on every provider, in **both** directions:
  `Write(string)`/`WriteBytes` and `ReadString()`/`ReadBytes(0)` must all be byte-transparent — never
  route the default read through a strict/ASCII text decoder (that throws on the first byte `>= 0x80`,
  breaking screen captures and Modbus frames). On NI, the untermed read loops `RawIO.Read` and decodes
  Latin-1 rather than calling `RawIO.ReadString()`; if you must read a large binary block you may also
  pass an explicit `maxBytes` to `ReadBytes`.
- **Errors:** wrap backend failures in `GpibException` and fill in `DescribeError` so callers get a
  decoded `GpibStatus` rather than a vendor stack trace. The status is folded into
  `GpibException.Message` (and, when undecoded, the inner vendor message), so every path that prints the
  message surfaces the real cause — you do not need each caller to read `.Status` explicitly.
- **Availability over exceptions:** a stub or a provider whose driver is absent should return
  `IsAvailable == false` with a clear `UnavailableReason`, not throw from the constructor.

## Vendor assemblies and the NI project

The core `GpibUtils.Visa` assembly has **no VISA/vendor dependency** — it's the abstraction, the
stubs, and the simulator, and it builds anywhere. Vendor-specific providers live in their own projects
so they can be referenced (or dropped) independently:

- **`GpibUtils.Visa.Ni`** holds the NI providers and references the **official NI/IVI VISA.NET
  assemblies** — `Ivi.Visa.dll` and `NationalInstruments.Visa.dll` — installed by **NI-VISA**. On .NET
  Framework these are *not* distributed via NuGet, so they are resolved from the local install. The DLLs
  are never committed, and a machine without NI-VISA still builds (see below).

  **Resolution is deliberately version-agnostic.** NI-VISA registers both assemblies in the GAC (with
  `policy.*` publisher-policy assemblies that forward-redirect older references), *and* drops them on
  disk under version-named folders such as `NI VISA.NET 26.0`. The project therefore declares them by
  **simple name with no `HintPath` and no version**, and layers its discovery:

  | Layer | Mechanism |
  |---|---|
  | 1 | Explicit override — `IviVisaNetDir` / `NiVisaNetDir` (odd installs, CI images). Wins outright. |
  | 2 | The **GAC** — the normal case, and immune to NI upgrades. |
  | 3 | **Disk**, discovered by unversioned glob of the `NI VISA.NET *` and `VISA.NET Shared Components *` folders (64- and 32-bit), appended to `AssemblySearchPaths`. |

  Never pin a version-named folder here. Doing so means the next NI release silently reverts the build
  to the "NI-VISA unavailable" stub on a machine that *has* NI-VISA installed — a quiet failure that
  only `-p:RequireNi=true` surfaces. That is exactly what happened with a pinned `NI VISA.NET 26.3`.

  When nothing is found the project builds an **"NI-VISA unavailable" stub** so the whole solution and
  CI compile with zero NI setup; pass **`-p:RequireNi=true`** to hard-fail instead. The build prints
  which layer satisfied it.
- The core registry **reflection-loads** the NI providers when `GpibUtils.Visa.Ni` is deployed, so
  referencing that project is all it takes to get `NI-VISA` as the default — with no NI dependency
  leaking into the core.

Put your own vendor project (e.g. `GpibUtils.Visa.Keysight`) alongside `GpibUtils.Visa.Ni` and either
register from your app or add it to the reflection-load list in `GpibProviders`.

## The built-in providers

| Name | Lives in | Status | Notes |
|---|---|---|---|
| `NI-VISA` | `GpibUtils.Visa.Ni` | **Implemented (default)** | Official NI VISA.NET (`NationalInstruments.Visa` + `Ivi.Visa`); GPIB/USB-TMC/LXI/serial by resource string. |
| `NI-488.2` | `GpibUtils.Visa.Ni` | **Implemented (opt-in build)** | Native board/primary/secondary addressing; compiled only with `-p:DefineConstants=NI4882` + the NI-488.2 driver. |
| `Keysight-VISA` | `GpibUtils.Visa` | Stub | Placeholder for pinning to Keysight's IVI.NET. |
| `Prologix` | `GpibUtils.Visa` | Stub | `++` serial/TCP command set; skeleton documented in the class. |
| `AR488` | `GpibUtils.Visa` | Stub | Arduino controller emulating the Prologix command set over USB serial. |
| `Simulated` | `GpibUtils.Visa` | **Implemented** | In-memory fake instruments for hardware-free build/test/CI. |
