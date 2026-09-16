# Handoff

Oriented for picking this project up cold in a different editor/tool. Read this first, then
`docs/STATUS.md` (full current state, feature by feature) and `docs/aetherphone-screen-reference.md`
(design research notes) as needed. Nothing here duplicates those — this is the fast-orientation
layer.

## What this project is

A from-scratch rebuild of Linkpearl, an FFXIV Dalamud plugin — an in-game phone/communicator.
Not a fork, not a port: the old codebase (a hard fork of a project called Aetherphone) was
86%-identical to its upstream at the file-path level, so this is an independent reimplementation
built from a functional spec, not a refactor of that code.

## Standing rules — read before touching anything

These came from the user directly and apply project-wide, not just to the request that produced
them:

1. **No Aetherphone, Aetheros, or old-Linkpearl code, ever.** Reading those as a *behavioral*
   reference (to understand what a feature does, then independently design how to build it) is
   fine. Copying, porting, translating, or mechanically renaming their code is not, under any
   circumstance. This includes net/auth clients, UI, and crypto. Pearlgate contracts in this
   workspace are the API spec to implement against, not a license to lift Aetheros.
2. **No AI in the product identity — and that extends to git.** The phone itself must never
   present as an AI product (no "AI-powered", no AI branding/copy/naming). This also covers the
   repository: no AI-tool mentions in commit messages, no Co-Authored-By trailers, no
   "Generated with X" footers, ever, regardless of what any tool's default commit convention is.
3. **Git identity is `Pearlgate-XIV <Pearlgate-XIV@users.noreply.github.com>`**, set as a **local**
   override in this repo's `.git/config` (global git identity on this machine is a different
   account for other projects — don't touch the global config). All existing history was rewritten
   to this identity before anything was ever pushed; nothing has been pushed to `origin` yet.
4. **Version bumps are rare and deliberate.** `Directory.Build.props`'s `<Version>` moves only on
   real milestones, never automatically. It's currently `0.1.0.0` — leave it there unless
   explicitly asked to bump it.
5. **Only edit `docs/STATUS.md` to add real, verified changes** — it's written in a
   "what's actually built and verified" voice, not aspirational. When a gap gets closed, the old
   gap bullet gets replaced/marked done, not left stale alongside a new one saying the same thing.

## Working from another computer

1. **This chat.** Sign into the **same Cursor account** on the other machine. Open this
   repo and look for this conversation (chassis skins, Apps grid, desktop zip). The thread
   follows the account; it is not inside the dll zip.
2. **The source.** Clone `https://github.com/Pearlgate-XIV/Linkpearl.git` (or pull). Debug
   assembly name is `LinkpearlDev`. Read this file, then `docs/STATUS.md`.
3. **The plugin zip.** `LinkpearlDev.zip` on the Desktop is a loadable **Linkpearl (rebuild
   dev)** snapshot for another machine or another person. In `/xlplugins` add
   `LinkpearlDev.dll` as a Dev Plugin Location. Leave the old plugin named just **Linkpearl**
   (Aetherphone fork) disabled.
4. **Tell the next agent:** version stays `0.1.0.0`; no Aetherphone, Aetheros,
   or old-Linkpearl code; git identity is the local `Pearlgate-XIV` override; no AI mentions in
   commits.

## Architecture in one paragraph

Layered projects, dependencies point one direction only: `Linkpearl.Abstractions` (contracts +
pure data, zero Dalamud dependency) → `Linkpearl.Canvas` (ImGui-backed implementations of those
contracts) + `Linkpearl.Data` (JSON section files under `state/`) → `Linkpearl.Device` (chassis, window, shell chrome) + `Linkpearl.Destinations`
(the four screens — both depend on Abstractions, neither depends on the other except Device
depending on Destinations to display them) → `Linkpearl.Net` (Pearlgate HTTP client, Abstractions
only) + `Linkpearl.Platform.Ffxiv` (Dalamud game-state adapters behind interfaces) →
`Linkpearl.Applets.Core`/`Linkpearl.Applets.Life` (a separate, currently-dormant "installable
mini-app" model — see below) → `Linkpearl.Host` (composition root, `HandsetHost`, `Plugin.cs` —
the only place that knows every concrete type).

**Chrome that is actually on screen:** left `DestinationDock` (Messages / You / Explore /
Settings), right `AppsDock` (Life apps carousel), bottom `SoftKeyBar` (Recents | Home diamond |
Back). Bundled chassis skins (`Chassis/phone.png`, `tablet.png`, etched and back-plate pairs) draw over a transparent window
with an opaque body fill so the world does not show through the glass hole. Tune is Settings.
`SettingsApplet` still compiles and is unused.

## Build

```bash
export DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev   # or wherever your Dalamud dev install lives
cd ~/Linkpearl
dotnet build Linkpearl.slnx -c Release            # the shippable build
dotnet build src/Linkpearl.Host -c Debug          # produces LinkpearlDev.dll/.json for dev-plugin loading
```

Both configs currently build with 0 errors, ~83 warnings (all pre-existing, stylistic: `CA1051`
visible-field warnings on small geometry value-types, one `CA1711` naming nit on `RouteStack`,
one `IDE0005` doc-file nag). Don't chase these down as a project unless asked — they're known and
accepted, not regressions to fix reflexively.

## What to tell the agent

Two different asks. Use the matching line.

**Will it compile?** Paste: *Build Debug and tell me if it succeeds.* That should run
`DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev` and `dotnet build src/Linkpearl.Host -c Debug`.
You want 0 errors; existing style warnings are known.

For the shippable assembly (`Linkpearl.dll`): *Build Release and tell me if it succeeds.*

**Rebuild the phone you load in-game?** Paste: *Build Debug so I can reload LinkpearlDev in
/xlplugins.* That writes `src/Linkpearl.Host/bin/Debug/LinkpearlDev.dll`. Then in-game:
`/xlplugins` → **Linkpearl (rebuild dev)** → disable → enable.

The agent cannot see the handset in FFXIV. “Check the build on the phone” here means compile
Debug, not click through the UI. After a reload, you look at it in-game.

**First message on another PC** (no history): *Read docs/HANDOFF.md then docs/STATUS.md. Build
Debug and tell me if it succeeds.*

## Loading it in-game (dev plugin)

Dalamud's Dev Plugin Locations entry for this rebuild is the Debug host output (Wine `Z:` is
`/`):

`Z:\home\cas\Linkpearl\src\Linkpearl.Host\bin\Debug\LinkpearlDev.dll`

Same file on Linux: `~/Linkpearl/src/Linkpearl.Host/bin/Debug/LinkpearlDev.dll`. Build with
`-c Debug` so wallpaper PNGs and the json sit next to that dll. Release output is a different
assembly name (`Linkpearl.dll`) and will not refresh this entry.

Dev plugin locations persist across game restarts and auto-load on launch — but **do not** get
picked up automatically if you rebuild the DLL while the game is already running; that needs a
manual toggle in `/xlplugins` (find the dev plugin entry, disable, re-enable) to force Dalamud
to re-read the file from disk.

**A real crash already happened and got fixed** (see `STATUS.md`): Dalamud constructs plugins off
the main thread, but most game-state reads (anything touching `IObjectTable`, likely others) are
main-thread-only. If you add a new `Platform.Ffxiv` adapter that reads game state eagerly in its
constructor, expect the same `InvalidOperationException: Not on main thread!` — defer any such
read to the first `IFramework.Update` tick instead, the way `FfxivGameSession.HandleFirstUpdate`
already does. Check `~/.xlcore/logs/dalamud.log` for the actual exception before guessing at
plugin-load failures; it's the source of truth, not speculation.

## Immediate next steps (ranked by what's probably most valuable)

1. Game talk is wired (party / tells / linkshells as one Messages inbox). Tells persist per
   character and each counterpart has a Profile. Next drill-down: Pearlgate chat **send**.
   Home/Social already list Pearlgate people and stories (or an honest empty state when that
   surface is off). `DemoData` is gone.
2. Tune look/plate/shape/presence persist on `HandsetConfig` with size, form, and the
   Pearlgate session token. Notes, universal/studio search drafts, and camera gallery
   stills survive unload under `state/` (`notes.json`, `search.json`, `photos/`). Alarms
   still use `alarms.json`. `Linkpearl.Data` owns those section files and schema stamps;
   Look is not migrated off `HandsetConfig`.
3. Pearlgate beyond REST lists: websocket/realtime, E2E chat keys, posting stories, Vybe.
   `Linkpearl.Net` already signs in, refreshes `/me` + chats + contacts + stories, and searches
   people.
4. Wallpaper is in: `ScreenField` + bundled day/night plates. Look persists on `HandsetConfig`
   after `FreshBoot` has advanced.

Full gap list with more detail is in `STATUS.md`'s "Known gaps" section — this is just the
short version for getting started.
