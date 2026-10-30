# Third-party software notices

The Halberion Nexus source-available license applies only as stated in
[`LICENSE`](../LICENSE). It does not replace or narrow licenses for the
third-party components below. The current direct and transitive NuGet graph
was inspected from the project files and restored package metadata on 30
October 2026. Recheck this inventory whenever dependencies change and before
each binary release.

## Sensor library and transitive components

| Component | Version | License | Source / license |
|---|---:|---|---|
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | [Source](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), [license](https://www.mozilla.org/en-US/MPL/2.0/) |
| BlackSharp.Core | 1.0.7 | MPL-2.0 | [Source](https://github.com/Blacktempel/BlackSharp), [license](https://www.mozilla.org/en-US/MPL/2.0/) |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | [Source](https://github.com/Blacktempel/DiskInfoToolkit), [license](https://www.mozilla.org/en-US/MPL/2.0/) |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | [Source](https://github.com/Blacktempel/RAMSPDToolkit), [license](https://www.mozilla.org/en-US/MPL/2.0/) |
| HidSharp | 2.6.4 | Apache-2.0 | [Project](https://software.seekye.com/hidsharp), [NuGet license text](https://www.nuget.org/packages/HidSharp/2.6.4/License) |
| Mono.Posix.NETStandard | 1.0.0 | Upstream repository marked MIT; package metadata redirects to the upstream Mono license file, which contains additional notices that need package-level review | [NuGet package](https://www.nuget.org/packages/Mono.Posix.NETStandard/1.0.0), [upstream repository](https://github.com/mono/mono.posix), [license file](https://github.com/mono/mono/blob/main/LICENSE) |
| System.CodeDom | 10.0.2 | MIT | [NuGet license](https://licenses.nuget.org/MIT) |
| System.IO.Ports | 10.0.3 | MIT | [NuGet license](https://licenses.nuget.org/MIT) |
| System.Management | 10.0.2 | MIT | [NuGet license](https://licenses.nuget.org/MIT) |
| System.Threading.AccessControl | 10.0.3 | MIT | [NuGet license](https://licenses.nuget.org/MIT) |

LibreHardwareMonitor's version-pinned upstream notice also identifies its
embedded `PawnIO.Modules` code as **LGPL-2.1**. This additional component is
covered by its own license and is not covered by the Nexus terms. See the
[v0.9.6 upstream third-party notices](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/THIRD-PARTY-NOTICES.txt).

The package graph also contains .NET runtime, targeting-pack, and build-task
packages. Binary release archives produced as self-contained executables must
retain the applicable Microsoft and third-party notices generated for the
specific target runtime. This repository currently prepares source and build
workflows; it does not include a binary release archive.

## MPL-2.0 distribution note

The app uses LibreHardwareMonitorLib and receives several MPL-2.0 libraries
transitively. The MPL allows a larger work to be distributed under additional
terms only if those terms do not restrict recipients' MPL rights. Apache-2.0
and MIT components also carry their own redistribution grants, and the upstream
notice lists LGPL-2.1 code embedded in LibreHardwareMonitorLib. The Halberion
license's third-party clause is intended to preserve those rights; however,
its no-sale rule for the combined Software should be checked by a qualified
lawyer before distributing a bundled executable. See
[Mozilla's MPL FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/), especially
the guidance for executable and larger-work distribution.

The app uses the sensor library for read-only hardware monitoring. No
third-party source files are copied into Nexus modules in the current tree.
