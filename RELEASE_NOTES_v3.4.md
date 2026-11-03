# v3.4 — Windows Service + GUI Installer (fork)

This is a **fork** of [skbskhud/p104-unlock-v3](https://github.com/skbskhud/p104-unlock-v3).
All original reverse-engineering, the tool, the Python toolchain, the methodology and the
documentation are by **skbskhud and contributors**. The upstream repository declares no
license; this fork preserves the original attribution and does not claim ownership.
See `NOTICE.md`.

## What this fork adds

- **Windows Service mode**: `-install-service`, `-uninstall-service`, `-service`.
- **GUI installer** `P104Gen2x4-Setup.exe`: requests administrator, embeds the tool + the
  two drivers, shows service/link status, installs/uninstalls the service, opens the log.
- Service is `AUTO_START`, runs as `LocalSystem`, recovery actions 5 s / 10 s / 30 s,
  log at `C:\ProgramData\P104Gen2x4\status.txt`.
- No scheduled task is created by the service installer. The old `-install` task mode is
  still available if you prefer it.

## Tested

- Windows 11 build 26200 x64, P104-100 (`10DE:1B87`), stock NVIDIA 582.66.
- Result: **Gen2 x4** (`LNKSTA=0x00001042`) + **x16 width declaration**
  (`LNKCAP=0x00454D02`); physical link remains **x4**.
- Service install/repair, stop/start, uninstall/reinstall, and `-bw` end-to-end verified.
- No VBIOS, EEPROM or fuse writes; power cycle reverts the volatile registers.

## Install

1. Download `P104Gen2x4-Setup.exe`.
2. Run as administrator.
3. Tick the BYOVD acknowledgement, then **Install / Repair**.
4. Verify:
   ```powershell
   sc.exe query P104Gen2x4
   & "C:\Program Files\P104Gen2x4\P104Gen2x4.exe" -check
   ```
5. Reboot once to confirm the service starts automatically.

## Uninstall

```powershell
& "C:\Program Files\P104Gen2x4\P104Gen2x4.exe" -uninstall-service
```

The policy registry keys are kept by default (add `-purgepolicy` to remove them).

## Assets

- `P104Gen2x4-Setup.exe` — GUI installer (tool + drivers embedded).
- `P104Gen2x4-Service-Source.zip` — full source (tool, installer, docs, build scripts).
- `SHA256.txt` — hashes for the assets.

## Security / antivirus

The tool loads **WinRing0 / ThrottleStop** (BYOVD-style kernel drivers) and writes volatile
PCIe registers. Windows Defender, SmartScreen, antivirus and anti-cheat software may flag
the installer or the tool. The installer is **unsigned** (no code-signing certificate).
There is no obfuscation; the installer is built from the source in this repository.
Verify `SHA256.txt` and build from source if you prefer.

## Credits

- **skbskhud and contributors** — original project and reverse-engineering.
- WinRing0 / OpenLibSys and ThrottleStop authors.
- The P104/P106 community whose earlier experiments made this possible.
