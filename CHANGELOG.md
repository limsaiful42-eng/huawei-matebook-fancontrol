# Changelog

## 1.4.0 - 2026-08-23

- Add independent fixed BIOS/EC targets for Fan 0 and Fan 1 while always writing both targets explicitly.
- Add an Apple-style synchronization switch, separate safe-target selectors, and an apply-fixed-speed action to the GUI.
- Display both independent request values alongside the two physical tachometer readings.
- Add `--fan0` and `--fan1` fixed-target options to the command-line launcher.
- Validate Fan 0 at 5100 request / 5160 RPM and Fan 1 at 3800 request / 3900 RPM, followed by successful vendor-mode restoration.

## 1.3.0 - 2026-08-23

- Redesign the GUI with a macOS-inspired light canvas, rounded cards, status pills, restrained system colors, and a custom fan app mark.
- Add high-DPI-safe metric, control, settings, and activity layouts while retaining the native Windows title bar and UAC behavior.
- Improve running and disabled control states and switch the activity log to a light appearance.
- Revalidate active quiet control with 13 samples, watchdog startup, automatic safe stop, and successful vendor-mode restoration.

## 1.2.0 - 2026-08-23

- Add a single-file Windows Forms GUI with live CPU temperature, both fan tachometers, BIOS/EC request, and logs.
- Add monitor-only, quiet automatic, timed full-speed, safe stop, and explicit vendor-restore controls.
- Add configurable full-speed duration and emergency-temperature controls.
- Add an external safe-stop signal and a global single-controller mutex.
- Verify the GUI at high DPI and complete an active-control test with 13 samples, watchdog startup, safe stop, and successful vendor-mode restoration.

## 1.1.0 - 2026-08-23

- Add a single-file `HuaweiFanControl.exe` launcher with embedded controller, watchdog, and default curves.
- Add an administrator execution manifest and avoid third-party PowerShell-to-EXE packers.
- Support quiet automatic, monitor-only, full-speed, timed, and custom-curve command-line modes.
- Rename console and log output from `TargetRPM` to `RequestedRPM` to reflect BIOS/EC request semantics.
- Add high-speed request calibration and document physical fan saturation around 7000–7200 RPM.
- Verify the EXE in one-minute monitor and active-control tests, including watchdog and vendor-mode restoration.

## 1.0.0 - 2026-08-08

- Initial PowerShell controller, independent watchdog, quiet-balanced curve, and failover validation.
