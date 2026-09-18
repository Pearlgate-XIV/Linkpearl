# Character state migration (v13)

LinkpearlDev **0.1.1.13** copies device-wide calendar, pearls, phone notes, friends stars,
market lists, and chat marks/cites onto **one** XIV character the player picks as **main**.
Nothing is claimed automatically on first login. The main character can be changed later.
Pearls from that import live on only one character at a time. There is one chooser (overlay
plus Settings → Profile). Secondary stores follow that same claim.

## Paths

| Role | Path |
| --- | --- |
| Legacy (kept) | `ConfigDirectory/state/calendar.json` |
| Legacy (kept) | `ConfigDirectory/state/pearls.json` |
| Legacy (kept) | `ConfigDirectory/state/handset-line.json` |
| Legacy (kept) | `ConfigDirectory/state/friends.json` |
| Legacy (kept) | `ConfigDirectory/state/market.json` |
| Legacy (kept) | `ConfigDirectory/state/chat-marks.json` |
| Legacy (kept) | `ConfigDirectory/state/chat-cites.json` |
| Character calendar | `ConfigDirectory/state/characters/{contentId:x16}/calendar.json` |
| Character pearls | `ConfigDirectory/state/characters/{contentId:x16}/pearls.json` |
| Character phone | `ConfigDirectory/state/characters/{contentId:x16}/handset-line.json` |
| Character friends | `ConfigDirectory/state/characters/{contentId:x16}/friends.json` |
| Character market | `ConfigDirectory/state/characters/{contentId:x16}/market.json` |
| Character chat marks | `ConfigDirectory/state/characters/{contentId:x16}/chat-marks.json` |
| Character chat cites | `ConfigDirectory/state/characters/{contentId:x16}/chat-cites.json` |
| Manifest | `ConfigDirectory/state/migration-v13.json` |
| Operator backup (3C) | `ConfigDirectory/migration-backups/v13-{UTC}/` |
| Operator backup (3D) | `ConfigDirectory/migration-backups/v13-secondary-{UTC}/` |

`contentId:x16` is the same 16-character lowercase hex TalkShelf uses (`talk/{id}.json`).
Content id `0` (logged out / unknown) has **no** character folder, **no** copy, **no** save,
**no** fallback to the legacy files.

Linux Debug ConfigDirectory is typically `~/.xlcore/pluginConfigs/LinkpearlDev`.

## First login overlay

Shown when a valid content id is present, this install has no main character, and Never
import has not been chosen. Character stores stay unbound until a main is chosen (Decide
later does not import Calendar, Pearls, Phone, Friends, Market, or chat marks/cites).
Buttons:

- **Use this character as main** — copy-forward each unclaimed legacy store onto that id.
- **Decide later** — leave sources alone; show the same explanation on the next valid login.
  Secondary stores stay unclaimed too.

**Never import** is Settings → Profile only (not on this overlay). It refuses every
character import, including secondary stores.

Copy on the overlay: those stores belong to one **main character**. They can choose that
character now (the one just logged in) or change it later in Settings → Profile.

## Settings → Profile

The Profile topic sits with the existing identity card (same portrait / name chrome as Home
and the Settings top-left face). Always shows the current main: the logged-in character’s
name when this character is main, otherwise a shortened content-id hex, or **Not set**.

- **Make this character main** — first time (unclaimed, not refused): same copy-forward as
  the overlay, including unclaimed secondary stores. After a main exists: **move** (not copy)
  Calendar, Pearls, Phone, Friends, Market, marks, and cites from the old main onto this
  character as **one set**. If this character already has **any** of those files, the move is
  blocked before anything is moved and the phone says that character already has data.
- **Never import** — only while the install is still unclaimed. No character import; each
  character uses its own empty defaults until someone is made main without a legacy copy
  (after Never).

Caption: the main can be changed later; only one character holds imported Pearls.

If a main is already chosen, that character may still claim leftover unclaimed secondary
legacy files on login (resume). Existing character files and the existing choice are not
overridden. Nothing is copied to every character.

## Manifest (`migration-v13.json`)

Atomic JSON. Fields (no balances, friend names, market items, messages, or cites):

- `Schema` — `1`
- `Claimant` — 16-char hex of the current main, or empty
- `Refused` / `Deferred` / `Complete`
- `Calendar`, `Pearls`, `HandsetLine`, `Friends`, `Market`, `ChatMarks`, `ChatCites` —
  `unclaimed` / `copied` / `exists` / `missing` / `malformed` / `failed`
- `Backup` — optional path string
- `AtUnix` — UTC unix seconds
- `Failures` — store file names that failed a write, if any

`Complete` is true only after durable character writes (or terminal skips) for this claim.
A failed store leaves `Complete` false so the next login of the **claimant** can resume.
Null or `unclaimed` secondary marks also resume for a chosen main.

Claimant is updated atomically after a successful copy-forward or after every dest file
from a reassign has been written and verified.

## Copy-forward (first main / resume)

Runs for the chosen main when the install is not refused. For each of the seven stores:

1. If the character file already exists, it **wins** (no overwrite).
2. If the legacy file is missing, status `missing`.
3. If the legacy file is not valid JSON, status `malformed`; original bytes stay; no dest.
   Malformed market does not touch Pearls.
4. Else deserialize the current schema, `AtomicJson` write, verify a JSON read, status `copied`.
5. Legacy files are **left in place**.

New character-folder writes never update the legacy files. One store failing does not block
the others on copy-forward. Logs are filename + exception type only (no friend names, market
items, chat, or cites). `.corrupt` sidecars stay as AtomicJson already writes them.

Pearls (play currency, not gil) copy only to the main character. Other characters do not
get a copy of that ledger. Switching A→B→A reloads disk; it does not import twice.

Friends keep starred `Name@World` keys and `OnlineFirst`. No name→id conversion. Live FFXIV
friend discovery and Pearlgate people are unchanged.

Market keeps watchlist and recents item ids. Universalis, pricing, and the game board are
unchanged. Lists refresh from the character file on switch. No merge across characters.

Chat marks and cites keep their schemas and the same content-id keying as chat. No PearlChat
transport, E2E, `chat-seal.json`, or TalkShelf changes. No cross-character marks.

A missing character file does not get empty friends/market/marks files, a welcome pearls
file, or an empty phone file until the player actually saves on that character.

## Move on reassign

When Settings → Profile makes a different logged-in character the main:

1. Flush in-memory stores for the current bind (atomically; skip creating files that do not
   exist yet). A failed outgoing save does not write into the incoming character’s files.
2. **Preflight:** if the new main already has any of the seven character files, **stop**
   before moving anything. Old main and Claimant stay. Message: that character already has
   data.
3. For each store: `AtomicJson` write dest from the old main’s file, verify a JSON read,
   then delete the old file only after dest verifies. Missing source is a skip. Malformed
   source fails the whole move; dests written in this attempt are removed; old files stay.
4. Update `Claimant` atomically to the new main. Legacy unscoped files are not touched.

Never two live Pearls ledgers from the same import: after a successful move the old main’s
`pearls.json` is gone. UI reloads one character’s set; it does not mix A and B.

## Friends / Market / chat (local only)

Starred friends and OnlineFirst, market recents/watchlist, and chat marks/cites are local
choices per character. A and B must not see each other’s. New characters start clean.

## Backup (operators)

Phase 3D deploy: copy only `friends.json`, `market.json`, `chat-marks.json`, `chat-cites.json`,
and `migration-v13.json` (from `state/` when present) into
`ConfigDirectory/migration-backups/v13-secondary-{UTC}/`. Do not recurse, overwrite, or
follow unexpected symlinks. If that backup fails, do not copy or touch the DLL.

## Rollback

1. Stop the plugin / close the game.
2. Restore the backed-up files into `state/`. Character folders may remain; older builds
   that only read legacy paths ignore them.
3. Replace the plugin with the matching older DLL if you need that code.

There is no in-phone cleanup button.

## Limitations

Gallery, music, VYBE, badges, alarms, notes/search, layout, notices, auth, secrets, seals,
and media cache stay device-wide. Talk remains on `state/talk/{hex}.json`. Malformed legacy
JSON is not imported. Content id is never inferred from name or Pearlgate.
