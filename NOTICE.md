# Notice & Attribution

This repository is a **fork** of:

- **skbskhud/p104-unlock-v3** — https://github.com/skbskhud/p104-unlock-v3

The original reverse-engineering, the Windows tool, the Python toolchain, the methodology
and the documentation are by **skbskhud and contributors**. All credit for the original work
belongs to them.

## What this fork adds

- Windows Service mode: `-install-service`, `-uninstall-service`, `-service`
- A GUI installer (`P104Gen2x4-Setup.exe`) that embeds the tool + drivers
- Release/build packaging and this notice

## License status

The upstream repository **does not declare a license**. This fork preserves the original
attribution and does **not** claim ownership of the original work. Do not assume that this
fork is MIT/GPL/BSD-licensed. If you are the original author and want this fork changed or
removed, please open an issue in this repository.

## Third-party components

- **WinRing0 / OpenLibSys** — kernel driver used for PCI config access.
- **ThrottleStop** driver — kernel driver used for physical memory mapping.

These are BYOVD-style (bring-your-own-vulnerable-driver) components. Windows Defender,
SmartScreen, antivirus and anti-cheat software may flag them. This is expected and is why
the project is distributed with full source and no obfuscation.

## Safety

The tool writes volatile PCIe configuration registers only. It does **not** write VBIOS,
EEPROM or fuses. A power cycle reverts the changes. Modifying PCIe links can still cause
device loss or system instability. Use it only on hardware you own, at your own risk.
