## Zandronum Watcher 0.1.0

Watches a handful of Zandronum servers and tells you the moment someone joins one, so you
stop refreshing a server browser waiting for a game to start.

![Overlay](https://raw.githubusercontent.com/masuexe/zandronum-server-watcher/main/docs/overlay.png)

### What it does

- Pins the servers you care about and shows live human counts, map and game mode in an
  always-on-top overlay
- Sends a Windows toast when an empty server gets its first player (bots are ignored)
- Sits in the tray, optionally starts with Windows, so it keeps running in the background
- Imports the custom-server list you already maintain in Doomseeker
- Also runs headless (`--headless`), writing alerts to a log file

### Install

No installer. Unzip anywhere and run `ZandronumWatcher.exe`. This build is
self-contained, so no .NET runtime is required.

Settings, logs and the pinned list are stored in `%LOCALAPPDATA%\ZandronumWatcher\`, so the
folder can be moved or replaced without losing anything.

**Requires Windows 10 (1809) or later, 64-bit.**

### First run

The overlay starts empty. Click **＋** → **Scan list** to fetch the public server list, then
click **＋** on the servers you want to keep. Or press **Import from Doomseeker** to copy the
servers you already added there.

### Notes

- 0.x means the configuration format may still change.
- A server that was queried seconds ago will not answer again immediately
  (`sv_queryignoretime`); it shows "no reply — retrying" and recovers on the next poll.
- Notification clicks do not open anything yet.
