# v3.4.1 - English installer text

Patch release for the Windows service installer. No PCIe or service behavior changes.

## Changes

- Removed the remaining Indonesian messages from the install and uninstall dialogs.
- The installer log is English-only. Link status is shown as `Link: Gen2 x4 (x16 declared)`.
- Windows service install/uninstall logs are English.
- Cleaned up labels and notices (no mixed-language text or em dashes).

## Assets

- `P104Gen2x4-Setup.exe` - GUI installer (tool + drivers embedded).
- `P104Gen2x4-Service-Source.zip` - full source.
- `SHA256.txt` - asset and embedded payload hashes.

## Notes

This is a fork of [skbskhud/p104-unlock-v3](https://github.com/skbskhud/p104-unlock-v3).
The upstream repository has no license; this fork keeps the original attribution and does not
claim ownership. See `NOTICE.md`.

The installer is unsigned and loads BYOVD drivers (WinRing0 / ThrottleStop). Antivirus and
SmartScreen may flag it. Verify `SHA256.txt` and build from source if you prefer.
