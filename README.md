# Halberion Nexus

**Unified system-control tools for Windows**

Halberion Nexus brings together system monitoring, hotspot controls, network
monitoring, device information, and storage tools in one Windows desktop app.
It is developed by **Azat Berkan Ölçer and Halberion Studio**.

The source is available under the custom **Halberion Nexus Source-Available
License** in [`LICENSE`](LICENSE). This is not an OSI-approved open-source
license. The text is prepared for independent legal review; it does not claim
guaranteed enforceability. Third-party components are governed by their own
licenses; see [`docs/THIRD-PARTY-NOTICES.md`](docs/THIRD-PARTY-NOTICES.md).

Official website: [halberion.com](https://halberion.com)

## Features

- **Home:** a dashboard for CPU and RAM, GPU load when available, drive space,
  network rates from the selected adapter, and module shortcuts.
- **Hotspot Control:** discovers network adapters, configures Windows Mobile
  Hotspot, and shows relevant Windows API and driver limitations. It does not
  reset PdaNet.
- **System Control:** CPU/RAM and process views, startup inventory, hardware
  information, available live sensors, GPU metrics where exposed, and optional
  game FPS capture through a separately installed PresentMon executable.
  Unsupported readings are not fabricated; the app does not change fan curves,
  voltage, or clocks.
- **Network Monitor:** selected-adapter upload/download rates and history,
  IPv4 TCP/UDP connection information by process, new-connection alerts, an
  explicit ping action, and confirmed reversible Windows Firewall app rules.
- **Device Control:** plain-language information about Bluetooth accessories
  such as mice, keyboards, and headsets, alongside a separate Device Manager
  inventory and audio, display, and power information.
- **Storage Manager:** drive usage and available health information, folder
  analysis, largest files, SHA-256 duplicate matching, and age-filtered
  temporary-file candidates. Cleanup requires confirmation and sends a chosen
  file to the Recycle Bin.
- Turkish and English interface, with automatic selection from the Windows
  display language.

## System requirements

- Windows x64. The app targets Windows APIs for build 19041 or later; the
  documented real-device checks were performed on Windows 11. Other Windows
  versions and hardware configurations have not been verified here.
- To build from source: .NET SDK 9.0.x and a Windows environment with the
  Windows Desktop targeting packs. The application projects target .NET 8 for
  Windows.
- A self-contained package includes its .NET runtime. No prebuilt download is
  currently published in this repository.

Sensor, Bluetooth, disk-health, and hotspot capabilities depend on Windows,
firmware, drivers, permissions, and the connected hardware. PresentMon must be
installed separately for FPS capture.

## Build and run from source

From a Windows PowerShell prompt in the repository folder:

```powershell
dotnet restore Halberion.Nexus.slnx --configfile NuGet.Config
dotnet build Halberion.Nexus.slnx -c Release --no-restore
dotnet run --project tests/Halberion.Nexus.Tests.csproj -c Release --no-build
```

To create a self-contained Windows x64 executable:

```powershell
.\scripts\package.ps1 -OutputDirectory .\artifacts\win-x64
.\artifacts\win-x64\HalberionNexus.exe
```

The output directory must be empty. The first restore downloads the pinned
NuGet dependencies. The published executable is unsigned.

## Usage notes and limits

- Choose the network adapter to monitor, including Wi‑Fi or PdaNet. Rates and
  history are adapter-wide. The current Windows connection API gives the app
  process and endpoint data but not per-process byte totals; this is not a
  GlassWire-equivalent per-app traffic ledger. IPv6 PID rows, packet capture,
  and DNS history are not provided.
- Windows Mobile Hotspot behavior depends on Windows and the Wi‑Fi driver.
  Actual PdaNet-to-phone internet routing must be checked with the target
  adapter and a physical phone.
- FPS capture requires a separately downloaded and selected PresentMon
  executable. Sensor values appear only when the hardware and driver expose
  them. The app does not promise FPS or RAM improvements.
- Hardware and network readings remain local. System and network changes are
  user initiated and confirmed; no automatic system tuning runs.
- The self-contained executable is unsigned and may show a Windows SmartScreen
  warning.

## Screenshots

No screenshots are currently included in the repository.

## Developer attribution

**Azat Berkan Ölçer and Halberion Studio**

Official website: [https://halberion.com](https://halberion.com)

## License

See [`LICENSE`](LICENSE) for the Halberion Nexus Source-Available License.
It permits free use, modification, and redistribution as stated
there, while prohibiting sale or license fees for the Covered Work. It is a
custom source-available license, not an OSI-approved open-source license, and
is prepared for legal review without a claim of guaranteed enforceability. The earlier MIT license commit remains in Git
history; this change does not claim to revoke rights already granted under
MIT. Third-party components remain under their own licenses.
