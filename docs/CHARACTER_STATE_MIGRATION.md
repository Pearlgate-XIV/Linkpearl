# Character state migration (v13)

LinkpearlDev **0.1.1.13** copies device-wide calendar, pearls, and phone notes onto **one**
XIV character the player picks as **main**. Nothing is claimed automatically on first login.
The main character can be changed later. Pearls from that import live on only one character
at a time.

## Paths

| Role | Path |
| --- | --- |
| Legacy (kept) | `ConfigDirectory/state/calendar.json` |
| Legacy (kept) | `ConfigDirectory/state/pearls.json` |
| Legacy (kept) | `ConfigDirectory/state/handset-line.json` |
| Character calendar | `ConfigDirectory/state/characters/{contentId:x16}/calendar.json` |
| Character pearls | `ConfigDirectory/state/characters/{contentId:x16}/pearls.json` |
| Character phone | `ConfigDirectory/state/characters/{contentId:x16}/handset-line.json` |
| Manifest | `ConfigDirectory/state/migration-v13.json` |
| Operator backup | `ConfigDirectory/migration-backups/v13-{UTC}/` |

`contentId:x16` is the same 16-character lowercase hex TalkShelf uses (`talk/{id}.json`).
Content id `0` (logged out / unknown) has **no** character folder, **no** copy, **no** save.

Linux Debug ConfigDirectory is typically `~/.xlcore/pluginConfigs/LinkpearlDev`.

## First login overlay

Shown when a valid content id is present, this install has no main character, and Never
import has not been chosen. Calendar, Pearls, and Phone notes stay unbound until a main
is chosen (Decide later does not import). Buttons:

- **Use this character as main** — copy-forward each unclaimed legacy store onto that id.
- **Decide later** — leave sources alone; show the same explanation on the next valid login.

**Never import** is Settings → Profile only (not on this overlay).

Copy on the overlay: those three stores belong to one **main character**. They can choose
that character now (the one just logged in) or change it later in Settings → Profile.

## Settings → Profile

The Profile topic sits with the existing identity card (same portrait / name chrome as Home
and the Settings top-left face). Always shows the current main: the logged-in character’s
name when this character is main, otherwise a shortened content-id hex, or **Not set**.

- **Make this character main** — first time (unclaimed, not refused): same copy-forward as
  the overlay. After a main exists: **move** (not copy) Calendar, Pearls, and Phone notes
  from the old main onto this character. If this character already has any of those files,
  the move is blocked and the phone says that character already has data.
- **Never import** — only while the install is still unclaimed. No character import; each
  character uses its own empty defaults until someone is made main without a legacy copy
  (after Never).

Caption: the main can be changed later; only one character holds imported Pearls.

## Manifest (`migration-v13.json`)

Atomic JSON. Fields (no balances, messages, or event bodies):

- `Schema` — `1`
- `Claimant` — 16-char hex of the current main, or empty
- `Refused` / `Deferred` / `Complete`
- `Calendar`, `Pearls`, `HandsetLine` — `unclaimed` / `copied` / `exists` / `missing` / `malformed` / `failed`
- `Backup` — optional path string
- `AtUnix` — UTC unix seconds
- `Failures` — store file names that failed a write, if any

`Complete` is true only after durable character writes (or terminal skips) for this claim.
A failed store leaves `Complete` false so the next login of the **claimant** can resume.

Claimant is updated atomically after a successful copy-forward or after every dest file
from a reassign has been written and verified.

## Copy-forward (first main)

Runs only when choosing the first main (overlay or Settings → Profile, install still unclaimed and not
refused). For each of the three stores:

1. If the character file already exists, it **wins** (no overwrite).
2. If the legacy file is missing, status `missing`.
3. If the legacy file is not valid JSON, status `malformed`; original bytes stay; no dest.
4. Else deserialize the current schema, `AtomicJson` write, verify a JSON read, status `copied`.
5. Legacy files are **left in place** for rollback to `b516606`.

New character-folder writes never update the legacy files. One store failing does not block
the others. Logs are filename + exception type only.

Pearls (play currency, not gil) copy only to the main character. Other characters do not
get a copy of that ledger. Switching A→B→A reloads disk; it does not import twice.

Calendar keeps titles, times, offsets, sources, and `LastFired` so old reminders are not
all fired on migrate. Venue puts go to the **active** character calendar.

Phone notes, recents, and contacts follow the active character. Own number still comes from
Pearlgate when signed in. No PearlChat merge.

A missing character file does not get a welcome/dev pearls file or an empty phone file until
the player actually saves on that character. That keeps Make-main from being blocked by
auto-created alt files.

## Move on reassign

When Settings → Profile makes a different logged-in character the main:

1. Flush in-memory Calendar, Pearls, and Phone notes for the current bind (atomically;
   skip creating files that do not exist yet).
2. If the new main already has any of the three character files, **stop**. Old main and
   Claimant stay. Message: that character already has data.
3. For each store: `AtomicJson` write dest from the old main’s file, verify a JSON read,
   then delete (or empty) the old file only after dest verifies. Missing source is a skip.
   Malformed source fails the whole move; dests written in this attempt are removed; old
   files stay.
4. Update `Claimant` atomically to the new main. Legacy unscoped files are not touched.

Never two live Pearls ledgers from the same import: after a successful move the old main’s
`pearls.json` is gone.

## Backup (operators)

Before deploying a new `LinkpearlDev.dll`, copy only those four names (legacy three +
manifest if present) into a new `migration-backups/v13-{UTC}/` folder under ConfigDirectory.
Do not recurse, overwrite, or follow unexpected symlinks. If that backup fails, do not copy
or touch the DLL.

## Rollback

1. Stop the plugin / close the game.
2. Restore the four files from the backup folder into `state/` (calendar, pearls,
   handset-line, migration-v13). Character folders may remain; `b516606` reads only the
   legacy paths.
3. Replace the plugin with the `b516606` build if you need that code.

There is no in-phone cleanup button.

## Limitations

Friends, market, chat marks, gallery, music, VYBE, badges, alarms, notes/search, layout,
notices, auth, and secrets stay device-wide. Talk remains on `state/talk/{hex}.json`.
Malformed legacy JSON is not imported. Content id is never inferred from name or Pearlgate.
