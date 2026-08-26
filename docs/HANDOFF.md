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

1. **No Aetherphone or old-Linkpearl code, ever.** Reading either as a *behavioral* reference
   (to understand what a feature does, then independently design how to build it) is fine and
   already happened repeatedly — see `docs/aetherphone-screen-reference.md` for the pattern. Copying,
   porting, translating, or mechanically renaming their code is not, under any circumstance.
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

## Architecture in one paragraph

Layered projects, dependencies point one direction only: `Linkpearl.Abstractions` (contracts +
pure data, zero Dalamud dependency) → `Linkpearl.Canvas` (ImGui-backed implementations of those
contracts) → `Linkpearl.Device` (chassis, window, shell chrome) + `Linkpearl.Destinations`
(the four screens — both depend on Abstractions, neither depends on the other except Device
depending on Destinations to display them) → `Linkpearl.Platform.Ffxiv` (Dalamud game-state
adapters behind interfaces) → `Linkpearl.Applets.Core`/`Linkpearl.Applets.Life` (a separate,
currently-dormant "installable mini-app" model — see below) → `Linkpearl.Host` (composition root,
`HandsetHost`, `Plugin.cs` — the only place that knows every concrete type).

**Two navigation models coexist, deliberately, mid-transition:**
- **Primary (active):** four fixed destinations — Home/Social/Explore/You — plus a central
  "crystal" button opening Universal Search. This is what's actually on screen. Lives in
  `Linkpearl.Destinations` + `Linkpearl.Device/Shell/{DestinationBar,QuickBar,
  UniversalSearchOverlay,HandsetShell}.cs`.
- **Dormant (compiles, unused):** an older "icon grid of installable apps" model
  (`HomeSurface`, `SoftKeyBar`, `RouteStack`, `IApplet`, three real apps: Clock/Calculator/
  Settings). Not deleted on purpose — it's the plumbing a destination will eventually need for
  its own drill-down navigation (e.g. Explore pushing a venue-detail screen), and Settings-style
  functionality has to live *somewhere* reachable, most likely surfaced from "You" or search.
  Don't be surprised these classes exist and aren't called from `HandsetShell` — that's correct,
  not an oversight, see the "Product direction pivot" note at the top of `STATUS.md`.

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

## Loading it in-game (dev plugin)

The user's FFXIV + Dalamud run on their own machine. Dev plugin locations persist across game
restarts and auto-load on launch — but **do not** get picked up automatically if you rebuild the
DLL while the game is already running; that needs a manual toggle in `/xlplugins` (find the dev
plugin entry, disable, re-enable) to force Dalamud to re-read the file from disk.

**A real crash already happened and got fixed** (see `STATUS.md`): Dalamud constructs plugins off
the main thread, but most game-state reads (anything touching `IObjectTable`, likely others) are
main-thread-only. If you add a new `Platform.Ffxiv` adapter that reads game state eagerly in its
constructor, expect the same `InvalidOperationException: Not on main thread!` — defer any such
read to the first `IFramework.Update` tick instead, the way `FfxivGameSession.HandleFirstUpdate`
already does. Check `~/.xlcore/logs/dalamud.log` for the actual exception before guessing at
plugin-load failures; it's the source of truth, not speculation.

## Immediate next steps (not yet started, ranked by what's probably most valuable)

1. Real functionality behind Social/Explore/You — right now only their first tab has content, and
   it's all `DemoData`.
2. A settings-persistence layer (`Linkpearl.Data`) — window size/form/search text and both
   preference objects (`DisplayPreferences`, `HandsetShapePreference`) currently reset every
   launch.
3. `Linkpearl.Net` — a real Pearlgate client. The backend already exists and is live (Kamatera
   box, `docker compose`, both `aetherchannel` and `pearlgate` stacks healthy as of this handoff),
   but nothing in this codebase talks to it yet.
4. A wallpaper system — see `docs/aetherphone-screen-reference.md`'s notes on async texture
   loading, cover-fit cropping, and brightness-driven legibility scrims for the shape a real
   implementation should take.

Full gap list with more detail is in `STATUS.md`'s "Known gaps" section — this is just the
short version for getting started.
