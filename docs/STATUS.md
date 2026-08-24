# Linkpearl rebuild: status

This is a from-scratch rebuild of Linkpearl. See the analysis report delivered alongside this
work for the full functional inventory (everything Aetherphone provides, everything current
Linkpearl provides, dependencies, and the target architecture). This file tracks what has
actually landed against that plan.

## What's built (Phase 0-1, partial Phase 2-3)

A compiling, layered solution that loads as a real Dalamud plugin and renders a themed handset
with a working home screen, one real applet, and soft-key navigation:

- **Linkpearl.Abstractions** — every contract: geometry, painting, text, input, theming, modules,
  applets, routing, persistence, platform (game session, chat, world services). Zero Dalamud
  dependency, zero mutable statics.
- **Linkpearl.Canvas** — the ImGui-backed implementation of those contracts: paint surface, font
  service (own size ladder, Inter faces as a placeholder type system), text painter, input probe,
  the default palette/theme, and two real layout primitives (Stack, TileGrid).
- **Linkpearl.Device** — the chassis (rail/frame/glass/screen geometry, square screen corners,
  two forms x six size steps), the shell (status strip, soft-key Recents/Home/Back bar, circular-
  tile home grid, route stack with back-stack semantics), corner-grip drag-to-resize (`ResizeGrip`
  — distance-ratio scaling, snaps to the nearest size step on release, cursor + grip-hint feedback
  while hovering or dragging), and the Dalamud Window that hosts it.
- **Linkpearl.Platform.Ffxiv** — `FfxivGameSession`, the first platform adapter, proving applets
  can depend on `IGameSession` without ever touching `Dalamud.*` types.
- **Linkpearl.Applets.Life** — `ClockApplet` + its module, proving the applet pipeline end to end:
  a module registers into DI, the host discovers it, the router opens it, it draws through the
  same `AppletFrame` contract every future applet will use.
- **Linkpearl.Host** — the composition root (`HandsetHost`) and `Plugin.cs`. This is the only
  project that knows every concrete type; everything below it only sees interfaces.

Verified by: `dotnet build Linkpearl.slnx -c Release` against `DALAMUD_HOME` pointed at a real
Dalamud install (0 errors on a from-clean build). The packager produces an installable
`Linkpearl/latest.zip` with manifest, icon, and fonts. Not yet verified: actually running inside
FFXIV (no game client available in this environment) — that check is still owed.

## What's deliberately not built yet

Everything else in the analysis report's phase list: `Linkpearl.Data` (sectioned settings +
migrations), `Linkpearl.Net` (Pearlgate client, realtime, crypto), `Linkpearl.Audio`,
`Linkpearl.Cinema`, the remaining applet families (System, World, Media, Social, Arcade), the
four apps to regain from Aetherphone (Casino, Coin, Housing, Hunts), localization catalogs, and
the platform-fake for headless testing. `ModuleDiscovery` is a hand-written list by design until
there are enough modules for reflection-based discovery to earn its cost.

## Known gaps to close before this is more than a proof

- Home screen is single-page (no paging/folders yet).
- Resize-drag works within a session but doesn't persist; `HandsetWindow.SetForm` (Pocket/Slate)
  still has no caller — only the six size steps are reachable, not the form switch.
- Chassis renders one finish (Crystal); the Etched art-panel finish is defined in
  `ChassisMetrics` but nothing selects it yet.
- No settings persistence at all: window size/form reset every launch.
- `PhoneGlow`, minimized device faces, recents overlay, and control center are not yet designed
  into the new architecture — the analysis report names them as Linkpearl-original systems to
  preserve, but no Device.* module implements them yet.
