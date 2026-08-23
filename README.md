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
