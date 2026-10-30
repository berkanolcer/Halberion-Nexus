# Build and validation guide

Run these commands on Windows from the repository root:

```powershell
dotnet restore Halberion.Nexus.slnx --configfile NuGet.Config
dotnet build Halberion.Nexus.slnx -c Release --no-restore
dotnet run --project tests/Halberion.Nexus.Tests.csproj -c Release --no-build
```

The GitHub Actions workflow builds and runs the automated checks on a Windows
runner. Hardware sensors, Bluetooth state, disk-health data, hotspot operation,
and PresentMon FPS capture depend on local hardware and drivers; CI cannot
confirm those behaviors on every user's PC. A real PdaNet-to-phone internet
route must be checked with a phone and the target Wi-Fi adapter.

No device-specific scan counts, local IP addresses, adapter names, or machine
logs are published in this guide.
