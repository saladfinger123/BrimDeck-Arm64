# Windows ARM64 fork

本仓库是 [saladfinger123/BrimDeck-Arm64](https://github.com/saladfinger123/BrimDeck-Arm64)。它保留 WPF / .NET 10，并在 `win-x64` 之外提供自包含的 `win-arm64`。它不替换 [MoFeng2223/BrimDeck](https://github.com/MoFeng2223/BrimDeck)：安装器标识、数据目录、更新仓库和传感器服务名都不同，两个产品可以并存。

用户把机器称为 “NVIDIA Spark”。这不是 CPU、GPU 或 SoC 型号，也不能据此写成 DGX Spark、GB10 或 Linux。构建目标是 Windows ARM64。早先把目标写成 Snapdragon、Adreno、Qualcomm 是未经核实的假设，已经撤回。下面的结论不依赖某一款 NVIDIA 设备。

## 已经实现的

| 范围 | 行为 |
| --- | --- |
| 应用 | WPF，`net10.0-windows`，自包含。不裁剪，不用 NativeAOT。指定 RID 时仍做 ReadyToRun。 |
| x64 | 继续构建和发布。只有 `win-arm64` 或 `Platform=ARM64` 时 `PlatformTarget` 才是 ARM64。 |
| CPU、内存、网速、磁盘、进程 | 仍走原来的 Windows 接口。这些读取用到的 64 位结构位移用 `Marshal.SizeOf` / `OffsetOf` 回归。 |
| 网速 | 间隔超过 5 秒，或计数器回退，记为不可用。不显示成 0，也不把睡眠时间平均成当前速度。 |
| 界面投递 | 只保留一份待显示的快照。新采样替换还没画上的旧采样。停止时清掉待显示的快照。 |
| 更新 | 清单的 `product` 必须是 `BrimDeck-Arm64`，`repository` 必须是 `saladfinger123/BrimDeck-Arm64`。程序只下载与自己架构一致的 `win-x64` 或 `win-arm64` 包，并且文件名里要有这个架构。 |
| 安装载荷 | `ArchitecturesAllowed` 为 `arm64` 或 `x64os`，两种包互相拒绝对方的系统。 |
| 安装器 stub | Inno Setup 7.1.0 编出来的是 x64 stub（`SetupArchitecture=x64`）。在 ARM64 Windows 上这个 stub 走模拟，它不是原生 ARM64 安装器。 |
| 用户数据 | `%LOCALAPPDATA%\BrimDeck-Arm64`。安装和升级不删除。卸载只有勾选后才删除。 |
| 内存模式 | 省内存、平衡、流畅都保持原样，包括空闲时的 GC 和工作集裁剪。没有同机测量，所以不改默认。 |

## 没有打开的

| 项目 | 原因 |
| --- | --- |
| Intel CPUID、MSR、AMD SMN | 这些是 x86 指令。ARM64 进程在调用前返回。 |
| ARM64 上的 PawnIO 和传感器服务 | 不安装、不启动。PawnIO 源码里有 ARM64 宏，不等于有签名驱动，也不等于适用于尚未核实的 CPU。服务只为 `win-x64` 发布，服务名是 `BrimDeckArm64Sensors`，不会卸掉上游的服务。 |
| ARM64 上的 `nvapi64.dll`、`nvidia-smi.exe` | x64 构建仍从驱动目录加载。没有核过这些文件的 Windows ARM64 PE，所以 ARM64 进程不加载。没有功率或温度时保持不可用。 |
| NVML | 本项目不调用 `nvml.dll`。`nvidia-smi` 在自己的进程里用 NVML，这不是 BrimDeck 的绑定。 |
| 显存文案 | 不从 NVIDIA 这个名字推断独显、共享或统一内存。DXGI 字段仍按 Windows 给出的口径。 |
| Authenticode | 没有配置证书。SmartScreen 可能提示。本 fork 不申请证书。 |
| 硬件结果 | Windows ARM64 的 CI 负责编译、测试和打包。它不测量用户的 GPU、传感器、电池或性能。 |

## 构建

在装有 .NET 10 SDK 的 Windows 上：

```powershell
dotnet build BrimDeck.sln -c Release
dotnet run --project tests/BrimDeck.Tests -c Release
dotnet publish src/BrimDeck/BrimDeck.csproj -c Release -r win-arm64 -p:Platform=ARM64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false
dotnet publish src/BrimDeck/BrimDeck.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false
./scripts/Publish-Release.ps1 -Version 1.4.0-arm64.1 -Runtime win-arm64
./scripts/Publish-Release.ps1 -Version 1.4.0-arm64.1 -Runtime win-x64
python scripts/audit_publish.py --dir <发布目录> --expect arm64 --out payload-audit.json
```

`Publish-Release.ps1` 会在安装包旁边写下 `publish-path.txt`。审计读的是那个发布目录。GitHub Actions 做同样的发布、审计，以及一次不安装传感器服务的当前用户安装，并检查另一架构的安装包会拒绝这台机器。产物在 workflow artifacts 里。Release 要等 release workflow 获得批准。

Linux 不能运行 WPF 或 Windows 测试。这两项在 Windows x64 和 Windows ARM64 的 runner 上跑。

## 还没有做的性能对比

在同一台 Windows ARM64 电脑上比，系统版本、电源模式、插电或电池、屏幕和打开的页面都要相同。A 是同一次提交的 `win-x64` 包在模拟下运行。B 是 `win-arm64` 包。之后每次只改一项。

把主程序，以及传感器服务或 `nvidia-smi`（如果真的启动了）分开记：CPU 时间、工作集、私有提交、唤醒、采样到界面的时间，以及读数是否仍和 Windows 计数器一致。空闲裁剪之后工作集变小，不能当成省下了内存。这些记录出现之前，不写百分比。帧率 ETW 的刷新间隔没有改，因为没有证明放慢之后 Present 计数还一样。

本文没有任何一个数字是实测结果。
