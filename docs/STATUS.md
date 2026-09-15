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
"fewer places to look, more things connected together." Life apps are reached from a
second right-edge handle (middle of the glass). Tapping it carousels the content to an Apps
screen (Clock, Notes, Alarms, Calendar, Calculator, Timer, Stopwatch, Weather, Eorzea, Camera,
Wallet / Pearls). Settings is a destination, not an
app icon. `HomeSurface` (icon grid) is what that Apps page draws. `RouteStack`/`IApplet` open
the tile the user picks.

## What's built

A compiling, layered solution that loads as a real Dalamud plugin and renders a themed handset
with the new four-destination UI, a Home dashboard, Social/Explore/You screens, and Universal
Search, all reading live Pearlgate data once you sign in:

- **Linkpearl.Abstractions** — every contract: geometry, painting, text, input (including the
  new `ITextField` for real keyboard text entry — the one widget needing OS-level focus/IME
  rather than custom hit-testing over draw-list paint), theming (`Palette` gained `WarmAccent`
  for the restrained gold/ivory detail the new visual direction calls for), modules, applets,
  destinations (`IDestinationScreen`, `DestinationTab`), routing, persistence, platform, plus
  `IPearlHub`/`PearlSnapshot` (the draw-thread-safe view of Pearlgate state), `DisplayPreferences`
  and the `Layout`/`Cards` primitives (`Stack`, `TileGrid`, `CardChrome` —
  the shared "smoked aetherglass" card background + hairline border every destination draws on).
  All pure `Rect`/data math, zero Dalamud dependency, zero mutable statics.
- **Linkpearl.Canvas** — the ImGui-backed implementation: paint surface, font service, text
  painter, input probe, `DalamudTextField` (the new real text-entry widget, via
  `ImGui.InputTextWithHint`), the default palette/theme.
- **Linkpearl.Destinations** (framework-agnostic): the four fixed destinations, now against
  `IPearlHub` instead of `DemoData` (that file is gone): `HomeDestination` is the phone hub
  laid out like the dashboard mockup — greeting over wallpaper (muted "Good evening," then the
  given name large, gold job/title, live weather + zone), a Linkpearl Announcements featured card
  (latest Pearlgate notice when signed in; tap opens the list), then a 2x3
  gold-edged grid (Messages / Party / Retainer / Friends Online / Market Watch / Event Starting).
  Home layering is status strip, scrolling dashboard, then the crystal Home button. There is no
  bottom action pill row. Cards that have
  Pearlgate or game data show it; party finder, retainers, market, and muster/yellow pages stay
  honest empty (same slot size as filled cards). Tapping a card opens the matching destination
  pane through `DestinationHub`. Weather is `IGameSession.WeatherName` (WeatherManager + the
  Weather sheet). `SocialDestination`
  (Feed = story rings, Messages = a unified talk inbox, People = party/tells plus Pearlgate
  contacts, Communities = honest empty because muster/yellow pages are off on this server; Phone =
  a keypad that opens a tell), `ExploreDestination` (For You = stories + people;
  Places/Activities/Events/Groups = honest empty for the same flags), `YouDestination` (live
  name/world/handle, Sign in / Sign out, followers/following/number). Tune (dock label Settings)
  has Touch and List: Touch inspects glass, rim, strip, pin, or ink (Flip for Presence); List is
  grouped rows with a live card. Choice persists as `HandsetConfig.TuneLayout`. Account mark
  opens You. `DirectorySearch` fills the crystal overlay from
  talks, chats, people, and `/users/search`; a tap opens the thread or profile.
- **Linkpearl.Net**: `PearlHub` + `GateClient`. HTTP to `https://pearlgate.194.113.211.29.sslip.io`
  on a worker thread, snapshot swapped under a lock for Draw. Sign-in is `POST /auth/challenge`
  then `POST /auth/verify` using the character name and world from `IGameSession`. Refresh every
  15s hits `/me`, `/chats/`, `/contacts/`, `/stories`, `/announcements`. Search is `/users/search?q=`. Sign-out is
  `DELETE /auth/token`. The bearer token is written back through `HandsetConfig.SessionToken` on
  the framework thread. No websocket, no E2E crypto. Pearlgate message **send** is still not
  wired; game talk (party, tells, linkshells) sends through `IChatBridge`.
- **Linkpearl.Device**: the chassis (rail/frame/glass/screen geometry, square screen corners,
  six size steps, corner-grip resize, right-edge power button); the primary shell pieces:
  `DestinationDock` (left-edge handle; destinations menu), `AppsDock` (right-edge handle;
  carousels to Apps), `SoftKeyBar` (Recents | Home diamond | Back). `HomeSurface` is a 5x6
  grid: 52-unit icons packed from the top, full cell width so labels are not cut off, 30 slots
  when filled. Chassis PNGs (`phone.png` / `tablet.png`) overlay the window with an opaque body
  under the glass hole. In-glass handles stay vertically centered; volume nubs are reserved.
  `StatusStrip` (time, crystal, and world on the left; decorative signal / wifi / battery marks
  on the right). `UniversalSearchOverlay` is still in the tree. `HandsetShell` orchestrates
  this. `RouteStack` opens Life applets from the Apps grid.
- **Linkpearl.Platform.Ffxiv**: `FfxivGameSession`, the first platform adapter. Resolves the
  character's home-world name from the Lumina `World` sheet, job name from `ClassJob`, and zone
  name from `TerritoryType`/`PlaceName` via `IDataManager`. `IsInParty` reads `IPartyList`.
  Game-state reads stay on the framework thread. `FfxivChatBridge` implements `IChatBridge`:
  it listens to `IChatGui.ChatMessage` for tells, party, alliance, linkshells, and CWLS, and
  sends by running the matching `/tell` `/p` `/a` `/lN` `/cwlN` line through
  `UIModule.ProcessChatBoxEntry` (the same entry the game chat box uses). Equipped linkshell names come from
  `InfoProxyLinkshell` / `InfoProxyCrossWorldLinkshell`. User bodies that start with `/` are
  stripped so the composer cannot fire an arbitrary text command.
- **Talk inbox** (`ITalk` / `TalkInbox`): one conversation list, not a channel-picker menu.
  Party (pinned while you are in one), alliance when grouped, each equipped linkshell, each
  tell as a 1:1 thread, plus Pearlgate chats in the same list (read-only until gate send exists).
  Social is now Direct / Messages / People / Feed / Phone (gold chrome, same language as Home). Tells
  live in Direct; party and equipped linkshells live in Messages. Phone sits on the far right as a
  reserved slot; it is not implemented yet. The left-edge menu uses those names, with Phone last. Tells persist per character under `state/talk/` keyed by `InfoModule.GetLocalContentId()` (not the zone object id), up to 400 lines, so history survives reload
  even when the other person is not on Pearlgate. Party/LS stay session-only. Each tell
  counterpart has a Profile (name, world, private note, saved count, Pearlgate handle/number
  when they match a contact). Open it from a thread header › or from People. People lists
  "Talked to" first, then nearby party, then Pearlgate contacts. Messages is an inbox plus a
  thread view with a real text field and Send (Enter also sends). Type a `First Last` or
  `Name@World` in the inbox filter to start a tell. Home Messages/Party cards, the dock Party
  item, and the Quick Bar still open the matching thread.
- **Linkpearl.Applets.Core** / **Linkpearl.Applets.Life** — Settings stays a destination, not an
  app icon. The Apps carousel (right-edge handle) is a 5x6 grid of Life applets: Clock (local +
  Eorzea bells), Alarms, Notes (in-memory scratch list), Calendar, Calculator, Timer, Stopwatch,
  Weather, Eorzea (live name/world/job/zone), Camera (zone still as a note), and Wallet (Pearls
  currency). `WalletModule` and `EorzeaModule` are on the hand-written `ModuleDiscovery` list and
  unhidden on `AppShelf` (they used to exist on disk but never reached the carousel). Not verified
  in-game.
  An open applet has a ‹ Apps row at the top that returns to that grid
  (Home/Back on the soft-key bar do the same; the right-edge handle leaves Apps entirely). `RouteStack.Home`/`Back`
  used to no-op after the first open because a present motion was started and never advanced.
- **Linkpearl.Host** — the composition root (`HandsetHost`) and `Plugin.cs`. This is the only
  project that knows every concrete type; everything below it only sees interfaces. `ITextureProvider`
  is injected here and handed to `DalamudTextureSource` so the screen field can load wallpaper files
  on the UI thread. `IDataManager` is handed to `FfxivGameSession`. `IChatGui` and `IPartyList`
  are handed to `FfxivChatBridge`. `PearlHub` and `TalkInbox` are constructed here and disposed
  with the host.

Verified by: `dotnet build src/Linkpearl.Host -c Debug` against `DALAMUD_HOME` (0 errors). In-game
sign-in against the live API is not verified in this environment.

**Screen wallpaper (own implementation, not a port):** the glass is no longer a flat `Palette.Surface`
fill. `ScreenField` cover-fits a bundled plate (`Wallpapers/lagoon-*.png`, `vine.png`, `grove.png`)
into the screen rect via `CoverFit.Uv`, layers the night image at a smoothed darkness
value, and paints a luminance-driven scrim (`LegibilityScrim`) so dashboard text stays readable.
Texture loads go through `DalamudTextureSource` (`ITextureProvider.GetFromFileAbsolute` +
`TryGetWrap`): missing files and load errors are remembered so a failed path is not retried every
frame; a still-loading wrap is treated as not-ready and the fallback fill stays visible. Darkness
follows the local clock across a one-hour dawn/dusk ramp (`DayNight.Darkness`), or You → Look can
force Day / Night / Auto. Appearance still resets every launch (no `Linkpearl.Data` yet). Not yet
verified in-game.

**Found and fixed via a real in-game load attempt:** `FfxivGameSession`'s constructor read
`IObjectTable.LocalPlayer` synchronously to catch up on a character already logged in when the
plugin loads. Dalamud constructs plugins off the main thread; that read (like most game-state
reads) is only safe on it, so the plugin threw `InvalidOperationException: Not on main thread!`
on every load attempt and never actually displayed anything — confirmed from `dalamud.log`'s
exception, not guessed at. Fixed by deferring the catch-up read to the first `Framework.Update`
tick (guaranteed main-thread) instead of doing it in the constructor. Checked the rest of the
Host/Platform.Ffxiv startup path for the same pattern — this was the only synchronous game-state
read outside a Draw/Update callback.

## What's deliberately not built yet

Pearlgate chat send, realtime (websocket) updates, E2E chat crypto,
AfterDark/Velvet UI, posting stories, Yellow Pages/Muster (those flags are off on the live
server, and the phone says so instead of inventing venues), `Linkpearl.Data` (sectioned settings
+ migrations; Look/clock format still reset every launch), `Linkpearl.Audio`, `Linkpearl.Cinema`,
the four apps to regain from Aetherphone (Casino, Coin, Housing, Hunts), localization catalogs,
and the platform-fake for headless testing. `ModuleDiscovery` is a hand-written list by design
until there are enough modules for reflection-based discovery to earn its cost. That list now
includes Eorzea and Wallet alongside Clock, Alarms, Notes, Calendar, Calculator, Timer, Stopwatch,
Weather, Camera, Market, Venues, and the rest of the registered Life modules. Settings is the Tune
destination. Casino, Coin, Housing, and Hunts are still unbuilt. Not verified in-game.

## Known gaps to close before this is more than a proof

- **Done:** Linkpearl Announcements. Pearlgate grew the missing HTTP (`GET /announcements` for
  signed-in players; staff `GET/POST/DELETE /pearlgate/api/announcements`) and a desk page to
  post them. The phone pulls the page on the 15s refresh, the Home featured card shows the
  latest (or an honest empty / sign-in line), and tapping it opens a list then a body view.
  Until the live API is deployed with those routes, the card stays empty after a 404. Not
  verified in-game.
- **Done:** Home spacing and bar layering were rebuilt to match the dashboard reference skeleton
  (status / hero+grid / crystal), not to clone filled mockup portraits or events.
  Outer pad is 18, card gap 11, hero ~28% of the glass, shared 2x3 cell height, gold hairline
  cards with hover glow. The bottom of the glass is only the crystal Home button. Empty
  retainer/market/event cards keep the same slot as live ones. Not verified in-game.
- **Done:** every destination now scrolls. `IDestinationScreen.Compose` returns the content
  height it actually drew (every implementation gets this for free from how far its `Stack`'s
  `Remaining` shrank — no extra bookkeeping needed); `HandsetShell` keeps one `ScrollState` per
  destination (so switching tabs preserves each one's own scroll position), clips to the
  viewport, shifts content by the current offset, and updates it from mouse-wheel input while
  hovering. A thin indicator on the right edge appears only when content actually overflows.
  Not yet verified in-game (no game client in this environment) — the wheel-direction convention
  in particular (`ScrollState.Update`) is a judgment call, not something I could visually confirm.
- **Done:** Universal Search results are tappable. A Talk or Player hit opens the matching
  thread or profile through `DestinationHub`; other hits still just close the overlay. No
  keyboard-driven navigation (arrow keys, Enter). Query text of two or more characters also hits
  `/users/search` when signed in.
- **Done:** tell history now keys off the character content id (`InfoModule.GetLocalContentId`),
  not `GameObjectId`, which changes on zone and left `Flush` writing to a throwaway file (or
  never writing if identity stayed unknown after the first framework tick). Incoming/outgoing
  tells still land in Direct; phone-sent tells remember the peer if the chat echo names you.
  Not verified in-game.
- **Done:** Settings (Tune) is either Touch or List, switched from chips in the Tune header and
  remembered as `HandsetConfig.TuneLayout`. Touch: inspect glass (plates, bring a picture,
  shade), rim (scale, pocket, Phone/Tablet, slim/wide rim), strip (bells, world/marks), pin, or
  ink (colorway, core, lettering); Flip for Presence. List: compact live card plus grouped
  Ink/Plate/Body/Strip/Pin/Presence rows. Account mark opens You. Words are ours: Ink, Plate,
  Shade, Bells, Body, Pocket, Rim, Pin, Presence. Not verified in-game.
- **Done:** phone size now has two ways to change it that agree with each other. Corner-grip
  drag (existing) and the Handset size stepper in Settings both read/write the
  same `HandsetShapePreference` (Abstractions), so dragging and the explicit control can never
  disagree about the current size. Moved `HandsetSizeCatalog`/`HandsetForm` down from
  `Linkpearl.Device` to `Linkpearl.Abstractions` (`Linkpearl.Chassis` namespace) to make this
  possible — same "pure data, zero Dalamud dependency, shouldn't require the Device layer" reason
  `Stack`/`TileGrid` moved earlier.
- **Done:** `HandsetForm` renamed `Pocket`/`Slate` → `Phone`/`Tablet` (plain English, matching how
  the feature is actually talked about), and the form switch now has a caller: a "Form" toggle
  sits on the Handset page in Settings, sharing the same `HandsetShapePreference` — so form
  and size are both explicitly reachable through the same two-control pattern.
- Size range widened to be comparable to what Aetherphone's own resize allows (~290-880 width,
  versus Aetherphone's continuous 240-900) — see `docs/aetherphone-screen-reference.md` for the
  three-way comparison this was based on (upstream Aetherphone's continuous soft-snap-near-preset
  design, the old Linkpearl fork's always-hard-snapped six steps, and this rebuild's current
  middle ground of continuous-while-dragging/discrete-on-release). The aspect ratio itself (9:16)
  was deliberately left alone — that's a documented, intentional break from Aetherphone's taller
  19.5:9-style proportions, not an oversight.
- **Done:** look, plate, shade, ink, clocks, presence, and shape persist on `HandsetConfig`.
  Loaded into `DisplayPreferences` / `HandsetShapePreference` at boot, written on change, close,
  and unload. Mid-drag writes are skipped so a live resize does not flush the file every
  frame. The Pearlgate bearer token still persists as `SessionToken` on the same object.
- **Done:** the handset can be locked in place. A small padlock sits in the **top-right chassis
  gutter** (outside the screen, beside the clock; accent when locked). Power stays on the
  upper-right rail. Tune's pin tray and the padlock both set `ImGuiWindowFlags.NoMove` and
  skip corner-grip resize. Unlock to move or corner-resize again. Tune's pin tray shares the
  same switch. Not yet verified in-game.
- **Done:** Phase 4 window behavior. Open and miniature positions are remembered separately
  (`HasOpenPos` / `HasPocketPos` on `HandsetConfig`). Minimize keeps the phone on that side and
  restores the exact open corner; the miniature can be dragged while locked or unlocked without
  stealing the slide-to-wake track. The pocket face uses the same chassis PNG and aspect as the
  open phone. The wake slider outline uses `WarmAccent` (theme + accent from Settings). Not yet
  verified in-game.
- **Done:** `DestinationDock.DrawerArea` no longer throws `ArgumentException` from `Math.Clamp`. The side
  menu is sized to the phone (one `RowUnits` row per item, not 2× content that is taller than the
  9:16 screen at every size step), then vertically centered in the space above the crystal. Origin
  uses `Scalar.Clamp`, which swaps inverted bounds instead of throwing, so float rounding cannot
  crash Draw. Confirmed from in-game stacks at default size; reload the plugin to pick up the fix.
- **Done:** chassis finish is selectable. Slim rim is Crystal; wide rim is Etched
  (`ChassisMetrics` frame weight). Tune's Body tray writes `HandsetShapePreference.Finish`.
- **Done:** the screen has a wallpaper field. `ScreenField` cover-fits bundled plates (Lagoon with
  a day/night pair; Vine and Grove as still photos, same file for both looks) or a custom plate
  copied into `ConfigDirectory/plates/`, cross-fades Lagoon from the local clock (or Tune → Day/
  Night/Auto), and dims with a luminance-based scrim multiplied by Shade. Tune's glass hotspot
  cycles Lagoon / Vine / Grove / Yours. Persisted. Not yet verified in-game.
- **Done:** Control Center (tap the status strip: Quiet, hush in duty, bells, world, marks,
  still motion, Recents), Recents (recent life apps), and Quiet (badges and chimes stay dark).
  Not yet verified in-game.
- Chassis materials are still flat AQUOS-style color, not the ornate gold/engraved look from the
  latest hardware design reference. By explicit direction, only shape/proportions (one
  side button) were taken from that reference for now; matching the actual finish needs real
  texture assets (a front bezel image + back-plate image), not hand-drawn vector shapes.
- Only one side button exists (right edge, power/lock). The reference shows more chassis detail
  (corner rivets, a back-plate design) not yet represented at all.
