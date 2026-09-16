# Linkpearl rebuild: status

This file is verified truth for the living tree, not a to-do list. Testers / assembly
version: **0.1.1.13**. `Directory.Build.props` `<Version>` on this line is `0.1.1.13`; do not
bump unless asked.

**Phase 1 user-confirmed (in-game, 2026-09-16):** the owner said the phone “everything looks
good.” Treat XIVAuth sign-in, Pearlgate send on listed chats, Look/token persist, and the
landed chrome below as user-confirmed. Do not open a “implement SendChat” or “Look resets
every launch” ticket.

## Product direction

Four fixed destinations — Home, Social, Explore, You — plus a central crystal for Universal
Search. Life apps are the right-edge Apps carousel. Settings is Tune, not an app icon.
`HomeSurface` is the Apps grid. `RouteStack` / `IApplet` open the tile the user picks.

## What's built

A compiling Dalamud plugin (`LinkpearlDev` in Debug) that paints the handset and talks to
live Pearlgate at `https://pearlgate.194.113.211.29.sslip.io`.

- **Linkpearl.Abstractions** — geometry, paint/text/input (`ITextField`), theming (`WarmAccent`),
  modules, applets, destinations, routing, persistence, platform, `IPearlHub` / `PearlSnapshot`,
  `DisplayPreferences`, `Stack` / `TileGrid` / `CardChrome`. Pure data; no Dalamud.
- **Linkpearl.Canvas** — ImGui paint, fonts, `DalamudTextField`.
- **Linkpearl.Data** — sectioned JSON under `ConfigDirectory/state/` (`FileSettings`, schema
  stamps). Notes (`notes.json`), search drafts (`search.json`). Camera gallery is
  `state/photos/` + `library.json`. Alarms stay on `alarms.json`. Look, plate, shade, ink,
  clocks, presence, shape, and the Pearlgate bearer stay on `HandsetConfig`. `FreshBoot`
  wipes once if the mark is below 3, then bumps it — that is not “reset every launch.”
- **Linkpearl.Destinations** — Home (greeting, announcements card, 2x3 grid, STORIES card),
  Social (Direct / Messages / People / Feed / Phone keypad), Explore (For You stories +
  people; Places/Activities/Events/Groups honest-empty while muster/YP flags are off), You
  (live name/world/handle, XIVAuth Sign in / Sign out). Tune is Touch or List
  (`HandsetConfig.TuneLayout`). `DirectorySearch` fills the crystal overlay.
- **Linkpearl.Net** — `PearlHub` + `GateClient` on a worker thread; snapshot swap for Draw.
  Sign-in is **XIVAuth** `POST /auth/xivauth/start` then poll `POST /auth/xivauth/poll`
  (browser confirm). Refresh (~15s) hits `/me`, `/chats/`, `/contacts/`, `/stories`,
  `/announcements`. Search is `/users/search?q=`. Sign-out is `DELETE /auth/token`. Bearer
  is `HandsetConfig.SessionToken`.
  - **Send:** `PearlHub.SendChat` → `POST /chats/{id}/messages`. `TalkInbox.Send` calls it
    for `pearl:` threads. `PearlSend.CanSend` is signed-in and not muted/banned; composer
    hints are SignedIn / muted / session-expired, not “send is not wired.”
  - **Create chat:** `POST /chats/` from Friends long-press, search Chat, or peer-sheet
    Message. Opens `pearl:{id}` on the existing send path.
  - **Realtime:** `GateRealtime` websocket `wss://…/rt` with bearer. REST refresh stays as
    fallback. Gate may still omit chat broadcast envelopes; the client applies them when
    present.
  - **Stories:** list / view / `PublishStory` (`POST /media` optional, then `POST /stories`).
    Explore rings, Feed rail, Home STORIES card. Fail closed when `StoriesLive` is false.
  - Game talk (party, tells, linkshells, Feed say/shout/yell) still goes through
    `IChatBridge`. **No E2E crypto.**
- **Linkpearl.Device** — chassis (six size steps, corner-grip, power nub), `DestinationDock`,
  `AppsDock`, `SoftKeyBar`, `StatusStrip`, `HandsetShell`. Chassis skins: Crystal (bundled
  AQUOS) and Etched (gold bezel + rivets) with matching back plates. Tune Body writes
  `HandsetShapePreference.Finish`. Android stays `android.png`. Wallpaper is `ScreenField`
  (Lagoon day/night, Vine, Grove, or custom plates). Control Center, Recents, Quiet, pin lock,
  separate open/miniature positions are in.
- **Linkpearl.Platform.Ffxiv** — `FfxivGameSession`, `FfxivChatBridge`. **No game-state reads
  in constructors** (defer to first `IFramework.Update`). Tells persist under `state/talk/`
  keyed by `InfoModule.GetLocalContentId()`.
- **Talk inbox** — one list: party, alliance, equipped LS, tells, Pearlgate chats (sendable
  when `CanSend`). Feed thread: typed `/emote`, `/dance`, `:wave:` go through
  `TrySendEmote`; right-click Target aims at that player. Start a Gate DM from Friends /
  search / peer sheet, not only by answering a listed thread.
- **Linkpearl.Applets.Core / Life** — Apps carousel from `ModuleDiscovery` (hand-written by
  design): Clock, Alarms, Phone, Notes, Calendar, Music, **VYBE**, Calculator, Timer,
  Stopwatch, Weather, **Eorzea**, Camera, Market, Venues, **Wallet / Pearls**, App Store.
  Settings stays Tune. Vybe (glass **VYBE** / **VYBE+**; leftover Gate path `/afterdark/*`)
  is a real applet: lanes, profile photo/banner upload through Pearlgate media. Staff
  notices: `StaffNoticeSheet` (OK dismisses warnings; bans stay locked).
- **Audio** — Host wires WASAPI / Icecast / radio (`PearlCommunityRadio`). Not an unbuilt
  project hole.
- **Linkpearl.Host** — composition root. Debug InternalName `LinkpearlDev`.

Verified by: Debug Host build (0 errors) and owner in-game confirmation that the phone looks
good. `dotnet build src/Linkpearl.Host -c Debug` with `DALAMUD_HOME` set
(`-p:EnableWindowsTargeting=true` on Linux).

**Main-thread load crash (fixed, verified from `dalamud.log`):** `FfxivGameSession` used to
read `IObjectTable.LocalPlayer` in the constructor. Dalamud constructs plugins off the main
thread. Catch-up is on the first `Framework.Update`. Do not reintroduce eager game-state
reads in constructors.

## Known gaps (still true)

- **E2E chat crypto** — not built. Do not port Aetherphone/Aetheros crypto.
- **Long-tail apps / shells** — `Linkpearl.Cinema`; Casino, Coin, Housing, Hunts; localization
  catalogs; headless platform-fake. `ModuleDiscovery` stays a hand-written list until a
  reflection catalog earns its cost.
- **Muster / Yellow Pages** — flags off on live Gate; phone stays honest-empty.
- **Optional:** two-client prove of Vybe/profile **banner** against live Gate
  (`POST /me/banner` / `bannerUrl`). Client already POSTs and reads `bannerUrl` /
  `coverUrl`. Live sslip.io may still 404 that route until Gate is deployed; not a phone
  rewrite.
- Extra chassis side affordances beyond the power nub (volume reserved, not a second live
  button). Shape/Crystal/Etched skins are already selectable.

Do not re-implement: Pearlgate send, XIVAuth, create-chat, `Linkpearl.Data`, `/rt` client,
stories UI, Vybe applet, Feed emotes, Crystal/Etched skins, Look persist, Wallet/Eorzea
discovery.
