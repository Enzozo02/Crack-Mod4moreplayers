# Changelog

## [1.0.1](https://github.com/Enzozo02/Crack-Mod4moreplayers/releases/tag/v1.0.1) - 2026-10-27
- Fix: bunk beds, player colours and ranking rows depended on each player's **own** `MaxPlayers`. A client with a lower value (e.g. host at 8, friend at 6) had no bunk for players 7-8, who then couldn't sleep, and showed repeated colours. They are now always built for 8 players. Only the host's `MaxPlayers` limits how many can join.

## [1.0.0](https://github.com/Enzozo02/Crack-Mod4moreplayers/releases/tag/v1.0.0) - 2026-10-27 (first stable release)
- **Up to 8 players**: the Steam lobby size, Mirror `maxConnections` and the in-game player slots (`GameManager.GetAvailablePlayerIndex`) follow `MaxPlayers` (1-8, default 8).
- **Double rooms**: players 5-8 get a bunk bed above players 1-4 (the static-batched bed mesh is rebuilt from data extracted from the game, with posts in the bed-frame colour), spawn on their bunk, and share the room's badge door and wardrobe. The door plate shows both names.
- **Unique colour per player**: shift summary cards, graph lines and egg blips (players 5-8: purple, yellow, pink, teal).
- **Lists that fit 8 players**: the end-of-shift *Team Performance* list and the dream lifetime board scroll by themselves. The *Employee Productivity Ranking* monitor shows pages of 4, with a `1/2` indicator.
- **MODS menu** in the main menu: lists every installed BepInEx mod with its image, title, version, author and description (`<Dll>.png` + `<Dll>.json`, or Thunderstore `icon.png` + `manifest.json`).
