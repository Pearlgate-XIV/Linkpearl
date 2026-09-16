# Handoff

Read this first when picking Linkpearl up in a new Cursor Project, machine, or chat.
Then `docs/STATUS.md` for feature inventory. Do not treat STATUS as a to-do list to
execute unprompted. Do not re-implement send, XIVAuth, create-chat, Data, `/rt`,
stories UI, Vybe, Feed emotes, Crystal/Etched, Look persist, or Wallet/Eorzea discovery.

## What this is

An independent FFXIV **Dalamud** plugin: an in-game phone. Display name **Linkpearl**.
Testers / Debug assembly **`LinkpearlDev`**. Official Dalamud already owns InternalName
`Linkpearl`, so this rebuild must **never** ship a listing or zip with that InternalName.

Not a fork. Do not copy, port, translate, or mechanically rename **Aetherphone**,
**Aetheros**, or the old Linkpearl tree. Behavioral reference only. Implement against
Pearlgate HTTP contracts and this repo’s types. If a feature exists upstream, design it
again here. Do not open those codebases to “match structure.”

Aetherphone’s in-phone chat app named “Linkpearl” is unrelated. It does not block
`LinkpearlDev` updates.

## People and accounts

- Product owner: **K.I.R.O.** (engineer; work in code, not slides).
- GitHub for this repo: **`Pearlgate-XIV/Linkpearl`**, default branch **`main`**
  (`origin` also has `master` for testers JSON that some installs still use).
- Git identity in **this repo’s** `.git/config` only:
  `Pearlgate-XIV <Pearlgate-XIV@users.noreply.github.com>`. Never change global git config.
- PRs / pushes that open a GitHub PR: **stop and ask** which account — **Kiro-XVI** or
  **Pearlgate-XIV**. Do not pick. Do not push to someone else’s origin.
- Staff testers/admins mentioned in product work: sleepy, caffeinektn, vale.

## Hard rules

1. No Aetherphone / Aetheros / old-Linkpearl source in this tree.
2. No AI branding in the phone. No AI tool names, Co-Authored-By, or “Generated with”
   in commits.
3. Version truth is **`0.1.1.13`** (`Directory.Build.props` `<Version>` and testers
   `linkpearl.json` / `pluginmaster.json`). Do not bump unless asked.
4. Do not commit secrets (Pearlgate `.env`, Icecast source password, session tokens).
5. **`src/PearlCount/`** is a private HUD experiment. If it appears untracked on disk,
   do **not** add it to testers zips, GitHub listings, or “ship the phone” unless asked.
6. Only edit `docs/STATUS.md` for verified, landed behavior.

## Repos and live services

| What | Where |
| --- | --- |
| Phone source | `https://github.com/Pearlgate-XIV/Linkpearl` |
| Pearlgate API / zip host | `https://pearlgate.194.113.211.29.sslip.io` |
| VPS | `194.113.211.29`, plugin files under `/opt/pearlgate/plugin-public` |
| Testers JSON (GitHub) | `https://raw.githubusercontent.com/Pearlgate-XIV/Linkpearl/master/linkpearl.json` |
| Testers JSON (Pearlgate) | `https://pearlgate.194.113.211.29.sslip.io/plugin/pluginmaster.json` |
| Testers zip | `https://pearlgate.194.113.211.29.sslip.io/plugin/LinkpearlDev.zip` |

Both JSON URLs must stay **the same listing** (`InternalName` `LinkpearlDev`, same
`AssemblyVersion` and zip URLs). Dalamud only updates a third-party plugin if
`InstalledFromUrl` still matches the repo URL used at install. Switching JSON = uninstall
then reinstall. Do not 404 `pluginmaster.json`.

Do **not** leave `Linkpearl.zip` or InternalName `Linkpearl` on the VPS or GitHub
testers listing.

## Architecture (where to edit)

Dependencies point one way. `Linkpearl.Host` is the only project that wires every
concrete type (`HandsetHost`, `Plugin.cs`).

- **Abstractions** — contracts, geometry, `DisplayPreferences`, `PearlSnapshot`, no Dalamud.
- **Canvas** — ImGui paint/text/fonts.
- **Data** — `state/` section files (notes, search drafts; photos catalog alongside).
- **Device** — chassis + shell. Home greeting lives in `StudioSurface` (honorific under the name).
  Tune Body selects Crystal / Etched skins.
- **Destinations** — Home / Social / Explore / You. Stories UI, Feed (incl. in-game emotes).
- **Net** — Pearlgate HTTP (`PearlHub`, `GateClient`) plus `GateRealtime` (`/rt`). Worker
  thread; snapshot swap for Draw. XIVAuth start/poll. `SendChat`, create-conversation,
  `PublishStory`.
- **Platform.Ffxiv** — game session + chat. **No game-state reads in constructors** (not on
  main thread). Defer to first `IFramework.Update` (`FfxivGameSession.HandleFirstUpdate`).
- **Applets.Core / Applets.Life** — Apps grid via `ModuleDiscovery` (Clock, Notes, **VYBE**,
  Eorzea, Wallet/Pearls, venues, Camera, …).
- **Host** — composition, `HandsetConfig`, Debug assembly name `LinkpearlDev`, audio/Icecast.

Chrome: left destination handle, right Apps handle, soft keys Recents / Home / Back.
Tune is Settings. Staff notices draw in `StaffNoticeSheet` (OK dismisses warnings;
bans stay locked). Optimistic local read + `KeepLocalStaffReads` in `PearlHub`.

Honorific on the glass is `display.OwnTitle` via `GlassName.Honorific` → `NameMark` /
`TitleFx`. It is **not** the Honorific Dalamud plugin unless the user says that.

## Testers ship (current)

- Listing and assembly version: **0.1.1.13**.
- Zip is kept small: skip `.pdb`, `libmp3lame*`, `Icons/emoji/`, `Icons/vybe-demo/`,
  `Icons/original/`, `Microsoft.Windows.SDK.NET.dll`, AudioHost extras as in
  `tools/publish-plugin.sh`.
- Pearlgate must serve the zip as **`application/zip`** and listings as
  **`application/json`**.
- Testers often open **source** (`…/src/Linkpearl.Host`) in `/xlplugins` instead of
  **`LinkpearlDev.dll`**. That will not load. Load the built DLL (or the custom-repo
  install).

Do not run `tools/publish-plugin.sh` unless asked. It builds, scps the zip + both JSON
names, uploads GitHub `dev` release, commits listings, and pushes **`master` and `main`**.

## Build

Linux (this machine is CachyOS):

```bash
export DALAMUD_HOME="$HOME/.xlcore/dalamud/Hooks/dev"
dotnet build src/Linkpearl.Host -c Debug -p:EnableWindowsTargeting=true
```

Output: `src/Linkpearl.Host/bin/Debug/LinkpearlDev.dll` + `LinkpearlDev.json`.

Windows:

```powershell
$env:DALAMUD_HOME = "$env:AppData\XIVLauncher\addon\Hooks\dev"
dotnet build src\Linkpearl.Host -c Debug
```

Wine Dev Plugin path example:
`Z:\home\<user>\Linkpearl\src\Linkpearl.Host\bin\Debug\LinkpearlDev.dll`

Release produces **`Linkpearl.dll`** (different InternalName). Do not point testers or
the Debug DevPlugin entry at Release.

Known: 0 errors, a pile of pre-existing style warnings (`CA1051` on geometry structs,
etc.). Do not “clean up” those unless asked.

## Load in-game

**Testers install (what most people use):** one custom-repo URL above → `/xlplugins` →
**Linkpearl**. Installed copy on this Linux box lived under
`~/.xlcore/installedPlugins/LinkpearlDev/<version>/`.

**Local Debug:** Dev Plugin Location = Debug `LinkpearlDev.dll`. Automatic reload watches
the DLL; Host pokes one byte after Linux builds because Wine’s watcher often misses a
replace. If it does not reload: `/xlplugins` disable/enable **Linkpearl**.

Do not run **two** `LinkpearlDev` copies (DevPlugin + testers install) at once.

Workspace rule about copying to a Windows OneDrive `LinkpearlDev` folder applies on the
Windows game PC, not this Linux desktop. Skip `libmp3lame*` on copy (files lock).

Owner confirmed in-game (2026-09-16) that the phone looks good. Phase 1 (XIVAuth, send,
Look persist) is user-confirmed.

## Immediate next (real unfinished work)

1. **E2E chat crypto** against Pearlgate contracts in this repo only. Do not port old trees.
   Do not block plaintext send until E2E is proven.
2. **Long-tail:** `Linkpearl.Cinema`; Casino, Coin, Housing, Hunts; localization catalogs;
   headless platform-fake. Muster/YP stay honest-empty while Gate flags are off.
3. **Optional:** two-client prove of profile banner on **live** Gate (`POST /me/banner`).
   Phone client already sends/reads the field; live route may still be missing.

Pearlgate send, create-chat, Data leftovers, websocket client, stories UI, and Vybe are
already in this tree. Next work is not a SendChat rewrite.

## Pearlgate / radio (do not paste passwords)

Phone talks HTTPS to Pearlgate. Icecast listen can stay HTTPS `/radio-stream`. SOURCE for
Alpha Channel is raw **`:8000`**. `ICECAST_SOURCE_PASSWORD` lives in Pearlgate `.env` —
never put it in chat or this file.

## First message for a new Project / agent

Paste:

> Read `docs/HANDOFF.md` then `docs/STATUS.md`. This is the Linkpearl Dalamud rebuild
> (InternalName `LinkpearlDev`, version 0.1.1.13). Do not copy Aetherphone/Aetheros/old
> Linkpearl. Do not publish testers or bump Version unless I ask. Build Debug with
> `DALAMUD_HOME` set and `-p:EnableWindowsTargeting=true` on Linux. Send, XIVAuth,
> create-chat, Data, realtime, stories, Vybe, Feed emotes, Crystal/Etched, Look persist,
> and Wallet/Eorzea are already landed — do not rebuild them.
