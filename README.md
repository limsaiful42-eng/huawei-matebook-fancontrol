# Huawei MateBook Fan Control

华为 MateBook 14 2024（FlemingH）双风扇自动控制器。已在 BIOS 1.22 上实机验证。

控制器调用固件自带的 `OemWMIfun` / `SFND` 路径，不写未知 EC RAM 地址，不修改或刷写 BIOS，也不需要内核驱动。

完整的逆向结论和本机验收结果见 `VALIDATION.md`。

> [!WARNING]
> 目前只在 Huawei MateBook 14 2024 / FlemingH、BIOS 1.22 上验证。其他机型、主板或 BIOS 版本可能使用不同接口。风扇控制失误可能导致过热、降频或硬件损伤；请先确认机型，并自行承担使用风险。

## 要求

- Windows PowerShell 5.1 或更高版本
- 管理员权限
- `root\wmi:OemWMIMethod` 中存在实例 `ACPI\PNP0C14\HWMI_0`

## 运行

### 图形界面（推荐）

从 GitHub Releases 下载 `HuaweiFanControlUI.exe`，双击并批准 UAC 即可使用，不需要安装或放置额外脚本。

界面提供四个操作：

- `仅监测`：只读取 CPU 温度与双风扇转速，不接管风扇。
- `安静自动`：使用已验收的温控曲线。
- `全速`：向 BIOS/EC 请求最高档；可设置分钟数，`0` 表示不限时。
- `停止并恢复原厂`：安全退出手动模式，让 BIOS 原厂策略重新接管。

运行中会实时显示 CPU 温度、Fan 0/Fan 1 物理转速、BIOS/EC 请求值和控制器日志。关闭正在控制风扇的窗口时，UI 会先等待原厂控制恢复。

### 命令行 EXE

从 GitHub Releases 下载 `HuaweiFanControl.exe`，双击后批准 UAC 即可运行安静自动曲线。EXE 内嵌控制器、watchdog 和默认曲线，不需要把 PowerShell 脚本放在旁边。

常用命令：

```powershell
# 安静自动曲线，持续运行到 Ctrl+C
.\HuaweiFanControl.exe

# 最高 EC 请求，默认五分钟后恢复厂商控制
.\HuaweiFanControl.exe --full-speed

# 最高 EC 请求，运行十分钟
.\HuaweiFanControl.exe --full-speed --minutes 10

# 只监测一分钟，不接管风扇
.\HuaweiFanControl.exe --monitor --minutes 1

# 查看全部参数
.\HuaweiFanControl.exe --help
```

EXE 的应用清单要求管理员权限，这是读取和调用 BIOS WMI 接口所必需的。当前发布文件没有 Authenticode 商业代码签名，Windows 可能显示“未知发布者”；请只从本仓库 Releases 下载，并使用随附的 `.sha256` 文件核对哈希。

### PowerShell 源码

在管理员 PowerShell 中进入克隆后的仓库目录：

```powershell
cd '.\huawei-matebook-fancontrol'
```

只监测、不接管风扇：

```powershell
.\HuaweiFan-AutoController.ps1 -MaxMinutes 1
```

应用安静均衡曲线：

```powershell
.\HuaweiFan-AutoController.ps1 -Apply
```

停止时按 `Ctrl+C`。正常退出、传感器错误、转速计异常或达到紧急温度时，脚本都会发出 BIOS 命令退出手动模式，让厂商自动控制重新接管。

临时全速运行五分钟：

```powershell
.\HuaweiFan-AutoController.ps1 -Apply -CurvePath '.\full-speed-curve.json' -MaxMinutes 5
```

这里显示的 `Request 12000` 是写给 BIOS/EC 的请求参数，不是转速计保证值。在实测机器上，物理风扇从 7400 请求提高到 12000 请求后只增加约 0–2%，稳定在约 7000–7200 RPM，偶发峰值约 7600 RPM。全速曲线仍保留 12000 请求，以获得可能的最高输出。

不要同时启动多个控制器实例。

## 默认曲线

曲线位于 `quiet-balanced-curve.json`：

| 起始温度 | 目标转速 |
|---:|---:|
| 0 °C | 3200 RPM |
| 58 °C | 3800 RPM |
| 64 °C | 5100 RPM |
| 70 °C | 6300 RPM |
| 76 °C | 7400 RPM |
| 82 °C | 9300 RPM |

升档立即生效；降档需要低于当前档阈值 3 °C，并连续满足三次采样，防止在边界频繁跳速。允许的目标值来自固件 FPS 档位表，最低使用已实测稳定的 3200 RPM。

## 安全机制

- 独立 PowerShell watchdog 每两秒检查主控制器进程与心跳；主进程崩溃或心跳超时会重试退出手动风扇模式。
- 任一风扇连续两次低于 1200 RPM，立即恢复厂商自动控制。
- 默认紧急温度为 85 °C，触发后立即恢复厂商自动控制。
- 每次运行在 `runtime` 目录保存 CSV 样本、JSON 汇总和 watchdog 日志。
- 强制结束主控制器的整个 PowerShell 宿主、强制关机或固件异常仍属于无法完全消除的风险；运行时不要删除 watchdog 进程。

项目附带 `Test-WatchdogFailover.ps1`，它会短暂提高转速并模拟主进程丢失，用于验证 watchdog 能独立恢复厂商自动模式；日常运行不需要执行它。

`Test-FanTargetCalibration.ps1` 会依次测试 7400、9300、12000 三个固件请求值，并计算每档最后五个样本的物理转速平均值。测试会持续约一分钟并短暂拉高风扇；仅在管理员 PowerShell 中、确认没有其他控制器运行时使用：

```powershell
.\Test-FanTargetCalibration.ps1
```

## 自定义

复制 JSON 曲线并修改后，用以下命令载入：

```powershell
.\HuaweiFan-AutoController.ps1 -Apply -CurvePath '.\my-curve.json'
```

温度必须严格递增，目标转速必须随温度不下降，并且只能使用控制器声明的固件档位。不要把低温档设为未经验证的低于 3200 RPM 值。

## 数据与隐私

运行日志写入本地 `runtime` 目录，该目录不会提交到 Git。仓库不包含 BIOS 镜像、ACPI 提取物、设备序列号或电源设置快照。

EXE 会将内嵌资源释放到 `%LOCALAPPDATA%\HuaweiFanControl\payload-1.2.0`，运行日志保存在该目录下的 `runtime`。不再需要时可以在控制器停止后删除整个 `%LOCALAPPDATA%\HuaweiFanControl` 目录。

## 构建 EXE

Windows 自带 .NET Framework C# 编译器即可构建，不需要下载第三方 PowerShell 打包器：

```powershell
.\Build-Exe.ps1
```

输出位于 `dist\HuaweiFanControlUI.exe` 和 `dist\HuaweiFanControl.exe`，同时为两者生成 SHA-256 校验文件。启动器源码和管理员权限清单位于 `launcher` 目录；构建时会把当前控制器、watchdog 和两条曲线作为资源嵌入 EXE。
