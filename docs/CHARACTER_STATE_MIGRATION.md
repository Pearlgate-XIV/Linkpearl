# Character state migration (v13)

LinkpearlDev **0.1.1.13** copies device-wide calendar, pearls, and phone notes onto **one**
XIV character the player picks. Nothing is claimed automatically on first login.

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

## Chooser

Shown when a valid content id is present and this install has no claimant and has not
chosen Never. Buttons:

- **This character** — copy-forward each unclaimed legacy store onto that id.
- **Later** — leave sources alone; ask again on the next valid login.
- **Never for this install** — no character imports; every character uses its own empty defaults.

Tune → General offers the same three actions while the install is still unclaimed (Later,
login screen, or Never not yet locked in as claimant). After a claimant is recorded, Tune
only shows that files follow the character.

## Manifest (`migration-v13.json`)

Atomic JSON. Fields (no balances, messages, or event bodies):

- `Schema` — `1`
- `Claimant` — 16-char hex, or empty
- `Refused` / `Deferred` / `Complete`
- `Calendar`, `Pearls`, `HandsetLine` — `unclaimed` / `copied` / `exists` / `missing` / `malformed` / `failed`
- `Backup` — optional path string
- `AtUnix` — UTC unix seconds
- `Failures` — store file names that failed a write, if any

`Complete` is true only after durable character writes (or terminal skips) for this claim.
A failed store leaves `Complete` false so the next login of the **claimant** can resume.

## Copy-forward

Runs only for the chosen claimant. For each of the three stores:

1. If the character file already exists, it **wins** (no overwrite).
2. If the legacy file is missing, status `missing`.
3. If the legacy file is not valid JSON, status `malformed`; original bytes stay; no dest.
4. Else deserialize the current schema, `AtomicJson` write, verify a JSON read, status `copied`.
5. Legacy files are **left in place** for rollback to `b516606`.

New character-folder writes never update the legacy files. One store failing does not block
the others. Logs are filename + exception type only.

Pearls (play currency, not gil) copy only to the claimant. Other characters get a normal
new purse (welcome / dev grant **once per character file**). Switching A→B→A reloads disk;
it does not import twice.

Calendar keeps titles, times, offsets, sources, and `LastFired` so old reminders are not
all fired on migrate. Venue puts go to the **active** character calendar.

Phone notes, recents, and contacts follow the active character. Own number still comes from
Pearlgate when signed in. No PearlChat merge.

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
