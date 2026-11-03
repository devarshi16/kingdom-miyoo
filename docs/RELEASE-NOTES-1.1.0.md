# 👑 Kingdom: New Lands 1.1.0 — SD-card installation and gameplay fixes

## 📦 Install by copying files

1. Extract `KingdomNewLands-MiyooMini-1.1.0.zip` onto your SD card root.
2. Copy your original `Kingdom_Data` folder into `Roms/PORTS/Games/KingdomNewLands/`, beside `knl.sh`.
3. Launch **Kingdom New Lands** from Ports. First launch prepares the files with a progress screen; our fresh test took about **12 minutes**. Press B to pause and launch again to resume.

You need your own supported PC copy of Kingdom: New Lands **1.2.8 / Unity 2017.1.2p1**. Original game files are not included. Back up `save/` before updating an existing installation.

[📖 Installation, controls and AI setup prompt](https://github.com/devarshi16/kingdom-miyoo/blob/v1.1.0/README.md)

## ✨ Changes since v1.0

- Preparation now runs on the Miyoo with progress, resumable checkpoints and recoverable installation. PC preparation remains optional.
- Repairs pooled enemy animation reuse that could leave living enemies invisible.
- Repairs tutorial/menu navigation, UI text layout, persistent day labels and the day-progress HUD.
- Restores catapult chassis rendering and adds the exercised wheel-joint support.
- Uses a coin counter, lighter decorative effects, bounded rain/snow and simple water detail.
- Optimizes collision queries and component lookups while keeping the 50 Hz physics step and original world-coin, arrow and siege logic.
- Adds clearer player instructions, an AI installation prompt and real-device installation/gameplay GIFs.

The runtime binaries are identical to the hardware-tested **1.1.0-rc6** build. Release 1.1.0 updates version metadata, documentation and presentation media.

## 🐛 Known limits

- **Heavy night battles can still slow down.** The crowded winter stress test measured about 11–19 FPS at stock 1200 MHz, with audio muted. This is not a 30 FPS combat release; audio-enabled FPS has not been established.
- Winter/red-moon and portal battles were exercised in isolated scenarios. A complete natural winter, long sessions and all later-island behaviors still need playtesting.
- Catapult joint/contact physics remain approximate; post-arrival drift may remain. The moving ground-tile contrast report is also still open.
- Linux PC preparation has been exercised. Windows/macOS preparation still needs validation. Game sound should be checked through a normal Ports launch; SSH tests with a paused Onion menu do not establish audible output.

[Detailed validation and measurements](https://github.com/devarshi16/kingdom-miyoo/blob/v1.1.0/docs/VALIDATION-1.1.0-rc6.md) · [Report a problem](https://github.com/devarshi16/kingdom-miyoo/issues)

For preparation errors, attach `logs/prepare.log`; for game errors, attach `logs/knl.log` from the game folder. Include Onion version, game source and reproduction steps. Review logs for personal paths before sharing.
