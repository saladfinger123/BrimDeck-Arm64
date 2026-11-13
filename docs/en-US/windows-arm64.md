# Windows ARM64 fork

This repository is [saladfinger123/BrimDeck-Arm64](https://github.com/saladfinger123/BrimDeck-Arm64). It keeps the WPF / .NET 10 app and adds a self-contained `win-arm64` build beside `win-x64`. It does not replace [MoFeng2223/BrimDeck](https://github.com/MoFeng2223/BrimDeck): the installer id, data folder, update repository and sensor service name are different, so both products can be installed.

The user described the machine as “NVIDIA Spark”. That name is not a CPU, GPU or SoC model, and it is not evidence for DGX Spark, GB10 or Linux. The build target is Windows ARM64. Snapdragon, Adreno and Qualcomm were an earlier unverified guess and are withdrawn. No claim below depends on a specific NVIDIA device.

## What is implemented

| Area | Behavior |
| --- | --- |
| App | WPF, `net10.0-windows`, self-contained. Not trimmed. Not NativeAOT. ReadyToRun stays on when a runtime identifier is set. |
| x64 | Still built and published. `PlatformTarget` is x64 unless the build is `win-arm64` or `Platform=ARM64`. |
| CPU, memory, network, disk, processes | Same Windows sources. 64-bit structure offsets used by those reads are checked with `Marshal.SizeOf` / `OffsetOf`. |
| Network rate | A gap longer than 5 seconds, or a counter that goes backwards, is unavailable. It is not shown as 0 and it is not averaged across a sleep. |
| UI delivery | One pending snapshot. A newer sample replaces an older one that has not been applied yet. Stopping clears the pending sample. |
| Updates | Manifest `product` must be `BrimDeck-Arm64` and `repository` must be `saladfinger123/BrimDeck-Arm64`. The copy downloads only the package whose `architecture` is its own `win-x64` or `win-arm64`, and the file name must contain that id. |
| Installer payload | `ArchitecturesAllowed=arm64` or `x64os`, so each package refuses the other OS. |
| Installer stub | Inno Setup 7.1.0 compiles an x64 stub (`SetupArchitecture=x64`). On ARM64 Windows that stub runs under emulation. It is not a native ARM64 installer. |
| User data | `%LOCALAPPDATA%\BrimDeck-Arm64`. Install and upgrade do not delete it. Uninstall deletes it only when the box is checked. |
| Memory modes | Saver, Balanced and Smooth are unchanged, including the idle GC and working-set trim. There is no same-machine measurement that would justify changing the default. |

## What is not enabled

| Item | Why |
| --- | --- |
| Intel CPUID, MSRs, AMD SMN | The instructions are x86. An ARM64 process returns before calling them. |
| PawnIO and the sensor service on ARM64 | Not installed and not started. PawnIO source containing an ARM64 macro is not a signed driver and not a module for an unverified CPU. The service binary is published only for `win-x64`, under the name `BrimDeckArm64Sensors`, so it does not replace upstream's service. |
| `nvapi64.dll`, `nvidia-smi.exe` on ARM64 | The x64 build still loads them from the driver directory. No Windows ARM64 PE of those files has been audited, so the ARM64 process does not load them. A missing power or temperature reading stays unavailable. |
| NVML | This project does not call `nvml.dll`. `nvidia-smi` uses NVML in its own process. That is not a binding in BrimDeck. |
| GPU memory wording | Dedicated, shared and unified memory are not inferred from the NVIDIA name. Windows DXGI fields stay labeled as Windows reported them. |
| Authenticode | No certificate is configured. SmartScreen can warn. This fork does not request a certificate. |
| Hardware results | CI on Windows ARM64 compiles, tests and packages. It does not measure the user's GPU, sensors, battery or performance. |

## Build

On Windows, with the .NET 10 SDK:

```powershell
dotnet build BrimDeck.sln -c Release
dotnet run --project tests/BrimDeck.Tests -c Release
dotnet publish src/BrimDeck/BrimDeck.csproj -c Release -r win-arm64 -p:Platform=ARM64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false
dotnet publish src/BrimDeck/BrimDeck.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false
./scripts/Publish-Release.ps1 -Version 1.4.0-arm64.1 -Runtime win-arm64
./scripts/Publish-Release.ps1 -Version 1.4.0-arm64.1 -Runtime win-x64
python scripts/audit_publish.py --dir <publish-folder> --expect arm64 --out payload-audit.json
```

`Publish-Release.ps1` writes `publish-path.txt` next to the installer. The audit reads that publish folder. GitHub Actions runs the same publish, audit and a per-user install that does not install the sensor service, and it checks that the other architecture's installer refuses the machine. Artifacts are workflow artifacts, not a Release, until the release workflow is approved.

Linux cannot run the WPF app or the Windows tests. Those run on the Windows x64 and Windows ARM64 runners.

## Performance comparison that has not been run

Compare on one Windows ARM64 machine, same OS build, power mode, AC or battery state, display, and the same pages and switches. Build A is this commit's `win-x64` package running under emulation. Build B is the `win-arm64` package. Change one thing at a time after that.

Record the app, and any sensor service or `nvidia-smi` process separately: CPU time, working set, private commit, wakeups, time from sample to UI, and whether the numbers still match the Windows counters. Do not treat a lower working set after the existing idle trim as memory saved. Do not report a percentage until those runs exist. The ETW frame-rate flush is unchanged, because a slower flush was not shown to keep the same present counts.

No number in this document is a measurement.
