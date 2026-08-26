# Linkpearl rebuild: status

This is a from-scratch rebuild of Linkpearl. See the analysis report delivered alongside this
work for the full functional inventory (everything Aetherphone provides, everything current
Linkpearl provides, dependencies, and the target architecture). This file tracks what has
actually landed against that plan.

## Product direction pivot (superseding the icon-launcher model below)

The original phase plan (and the "Home = icon grid of installable apps" section further down)
assumed a many-small-apps model, matching both Aetherphone and the old Linkpearl. That model has
been explicitly superseded by a UI concept reference: **four fixed destinations — Home, Social,
Explore, You — plus a central Linkpearl/crystal for Universal Search**, under the philosophy
"fewer places to look, more things connected together." Clock/Calculator/Settings still exist as
`IApplet` modules and still compile, but nothing in the primary UI opens them any more; they're
inert until something (most likely "You" or search) is built to reach them. `HomeSurface` (icon
grid) and `SoftKeyBar` (Android-style Recents/Home/Back) are no longer wired into `HandsetShell`
for the same reason — the destination-based nav directly replaces both. Neither class was
deleted: `RouteStack`/`IApplet`/`HomeSurface`/`SoftKeyBar` remain compiled and available for a
destination's own future drill-down navigation (e.g. Explore pushing a venue-detail screen).

## What's built

A compiling, layered solution that loads as a real Dalamud plugin and renders a themed handset
with the new four-destination UI, a working Home dashboard, a functional Universal Search
overlay, and a placeholder shell each for Social/Explore/You:

- **Linkpearl.Abstractions** — every contract: geometry, painting, text, input (including the
  new `ITextField` for real keyboard text entry — the one widget needing OS-level focus/IME
  rather than custom hit-testing over draw-list paint), theming (`Palette` gained `WarmAccent`
  for the restrained gold/ivory detail the new visual direction calls for), modules, applets,
  destinations (`IDestinationScreen`, `DestinationTab`), routing, persistence, platform, plus
  `DisplayPreferences` and the `Layout`/`Cards` primitives (`Stack`, `TileGrid`, `CardChrome` —
  the shared "smoked aetherglass" card background + hairline border every destination draws on).
  All pure `Rect`/data math, zero Dalamud dependency, zero mutable statics.
- **Linkpearl.Canvas** — the ImGui-backed implementation: paint surface, font service, text
  painter, input probe, `DalamudTextField` (the new real text-entry widget, via
  `ImGui.InputTextWithHint`), the default palette/theme.
- **Linkpearl.Destinations** (new project, framework-agnostic) — the four fixed destinations:
  `HomeDestination` (the modular dashboard: header, Up Next hero card, Messages/Party/Retainer/
  Friends/Market/Event cards, all realistic placeholder data), `SocialDestination` and
  `ExploreDestination` (each a demo feed under a genuinely clickable section-tab row — tapping a
  tab switches to it; only the first tab in each has real content, the rest show an honest "not
  built yet" placeholder rather than a tab that looks interactive and silently does nothing),
  `YouDestination` (profile + stat rows) — the latter three deliberately minimal per the design
  brief. Also `DemoData` (internal placeholder content) and `UniversalSearch` (the public, stable
  search facade other layers call — swapping in a live backend later only touches this one file).
- **Linkpearl.Device** — the chassis (unchanged: rail/frame/glass/screen geometry, square screen
  corners, six size steps, corner-grip resize, notch, power/lock side button); the **new**
  primary shell pieces: `DestinationBar` (the permanent four-tab bottom nav plus the central
  glowing crystal, replacing the old icon grid and Android-style soft keys), `QuickBar`
  (contextual strip for urgent items, occupies zero height when empty), `UniversalSearchOverlay`
  (the crystal's tap target: a real search box over `UniversalSearch`, recent searches shown
  when empty, tap-outside-panel or the × to close); `HandsetShell` rewritten to orchestrate all
  of this. The old `HomeSurface`/`SoftKeyBar`/`RouteStack` still compile, just aren't called from
  here any more (see the pivot note above).
- **Linkpearl.Platform.Ffxiv** — `FfxivGameSession`, the first platform adapter, proving applets
  can depend on `IGameSession` without ever touching `Dalamud.*` types.
- **Linkpearl.Applets.Core** / **Linkpearl.Applets.Life** — `SettingsApplet`, `ClockApplet`,
  `CalculatorApplet` still exist and still compile (proving the applet pipeline works), but are
  currently unreachable from the UI — see the pivot note.
- **Linkpearl.Host** — the composition root (`HandsetHost`) and `Plugin.cs`. This is the only
  project that knows every concrete type; everything below it only sees interfaces.

Verified by: `dotnet build Linkpearl.slnx -c Release` against `DALAMUD_HOME` pointed at a real
Dalamud install (0 errors on a from-clean build). The packager produces an installable
`Linkpearl/latest.zip` with manifest, icon, and fonts. Not yet verified: actually running inside
FFXIV (no game client available in this environment) — that check is still owed.

## What's deliberately not built yet

Real functionality behind Social/Explore/You (feeds, messaging, venues, profiles — everything
the design brief describes eventually living there), a live search backend (Universal Search
runs entirely against `DemoData`), `Linkpearl.Data` (sectioned settings + migrations),
`Linkpearl.Net` (Pearlgate client, realtime, crypto), `Linkpearl.Audio`, `Linkpearl.Cinema`, the
four apps to regain from Aetherphone (Casino, Coin, Housing, Hunts), localization catalogs, and
the platform-fake for headless testing. `ModuleDiscovery` is a hand-written list by design until
there are enough modules for reflection-based discovery to earn its cost. A path back to
Clock/Calculator/Settings (most likely from "You" or via search) doesn't exist yet either.

## Known gaps to close before this is more than a proof

- Home dashboard cards are hand-placed with fixed heights sized to their exact line count (see
  the comment in `HomeDestination.Compose`) — this is fragile by construction: a card that grows
  a line without its allocated height growing to match will silently overflow past its own
  background into whatever is next, since nothing clips or auto-sizes a card to its content yet.
  Verified numerically at every size step for the current content, not verified visually in-game.
- **Done:** every destination now scrolls. `IDestinationScreen.Compose` returns the content
  height it actually drew (every implementation gets this for free from how far its `Stack`'s
  `Remaining` shrank — no extra bookkeeping needed); `HandsetShell` keeps one `ScrollState` per
  destination (so switching tabs preserves each one's own scroll position), clips to the
  viewport, shifts content by the current offset, and updates it from mouse-wheel input while
  hovering. A thin indicator on the right edge appears only when content actually overflows.
  Not yet verified in-game (no game client in this environment) — the wheel-direction convention
  in particular (`ScrollState.Update`) is a judgment call, not something I could visually confirm.
- The Quick Bar now shows two demo items (with a badge) instead of one, but its "collapses when
  empty" behavior is still never actually exercised — the array is fixed, not driven by anything
  real, so the empty state has never been seen.
- Universal Search results are now tappable (hover highlight, click closes the overlay — see
  `UniversalSearchOverlay.Draw`) but there's still no keyboard-driven navigation (arrow keys,
  Enter). Selecting a result can't open anything real yet since no destination content exists for
  a player/venue/activity screen, so it only closes the overlay rather than faking a navigation.
- **Done:** phone size now has two ways to change it that agree with each other. Corner-grip
  drag (existing) and a new "Phone size" stepper in You's Display section both read/write the
  same `HandsetShapePreference` (Abstractions), so dragging and the explicit control can never
  disagree about the current size. Moved `HandsetSizeCatalog`/`HandsetForm` down from
  `Linkpearl.Device` to `Linkpearl.Abstractions` (`Linkpearl.Chassis` namespace) to make this
  possible — same "pure data, zero Dalamud dependency, shouldn't require the Device layer" reason
  `Stack`/`TileGrid` moved earlier.
- **Done:** `HandsetForm` renamed `Pocket`/`Slate` → `Phone`/`Tablet` (plain English, matching how
  the feature is actually talked about), and the form switch now has a caller: a "Form" toggle
  sits right below the size stepper in You, sharing the same `HandsetShapePreference` — so form
  and size are both explicitly reachable through the same two-control pattern.
- Size range widened to be comparable to what Aetherphone's own resize allows (~290-880 width,
  versus Aetherphone's continuous 240-900) — see `docs/aetherphone-screen-reference.md` for the
  three-way comparison this was based on (upstream Aetherphone's continuous soft-snap-near-preset
  design, the old Linkpearl fork's always-hard-snapped six steps, and this rebuild's current
  middle ground of continuous-while-dragging/discrete-on-release). The aspect ratio itself (9:16)
  was deliberately left alone — that's a documented, intentional break from Aetherphone's taller
  19.5:9-style proportions, not an oversight.
- Resize/form still don't persist across launches (no settings layer exists yet).
- Chassis renders one finish (Crystal); the Etched art-panel finish is defined in
  `ChassisMetrics` but nothing selects it yet.
- No settings persistence at all: window size/form/search-query reset every launch.
- No wallpaper/character-art system exists — Home's header sits on the flat theme background,
  not the illustrated backdrop the design reference shows behind the dashboard.
- `PhoneGlow`, minimized device faces, recents overlay, and control center are not yet designed
  into the new architecture — the analysis report names them as Linkpearl-original systems to
  preserve, but no Device.* module implements them yet.
- Chassis materials are still flat AQUOS-style color, not the ornate gold/engraved look from the
  latest hardware design reference — by explicit direction, only shape/proportions (notch, one
  side button) were taken from that reference for now; matching the actual finish needs real
  texture assets (a front bezel image + back-plate image), not hand-drawn vector shapes.
- Only one side button exists (right edge, power/lock). The reference shows more chassis detail
  (corner rivets, a back-plate design) not yet represented at all.
