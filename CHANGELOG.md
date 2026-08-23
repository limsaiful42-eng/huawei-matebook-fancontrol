# Changelog

## 1.1.0 - 2026-08-23

- Add a single-file `HuaweiFanControl.exe` launcher with embedded controller, watchdog, and default curves.
- Add an administrator execution manifest and avoid third-party PowerShell-to-EXE packers.
- Support quiet automatic, monitor-only, full-speed, timed, and custom-curve command-line modes.
- Rename console and log output from `TargetRPM` to `RequestedRPM` to reflect BIOS/EC request semantics.
- Add high-speed request calibration and document physical fan saturation around 7000–7200 RPM.
- Verify the EXE in one-minute monitor and active-control tests, including watchdog and vendor-mode restoration.

## 1.0.0 - 2026-08-08

- Initial PowerShell controller, independent watchdog, quiet-balanced curve, and failover validation.
