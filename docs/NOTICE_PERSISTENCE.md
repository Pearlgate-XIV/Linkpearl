# Notice dismissal persistence

Dismissed notices stay gone after a plugin reload. The tray is **rebuilt from live
sources**. Disk stores **compact fingerprints only** — never bodies, titles, previews,
toasts, Control Center layout, mini `pendingPocketNoticeId`, deep-link payloads, or a
full tray snapshot.

Path: `ConfigDirectory/state/notices.json` (Linux Debug:
`~/.xlcore/pluginConfigs/LinkpearlDev/state/notices.json`).

Atomic write (`tmp` + `File.Move`). Write on **dismiss or prune only**, never on load.
Missing file: empty set. Malformed / wrong schema: copy to `notices.json.corrupt` (or
`.corrupt.1`) and start empty. **FreshBoot does not delete** this file.

Owner of the RAM tray remains `NoticeLedger`. Owner of the file is `NoticeDismissalBook`.
Character rebind uses the **active** logged-in content id, not the main-character chooser.

## Producer inventory

| Kind | Producer | Id + generation | Event vs reusable | Destination | Scope | Rebuild-safe | Dismissal-safe |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Announcement | `PearlSnapshot.Announcements` | `ann:{id}:{CreatedAtUnix}` | Reusable announcement | Home → announcements | Device | Yes | Yes. New `CreatedAtUnix` is a new fingerprint. |
| Staff | unread `StaffNotices` | `staff:{id}:{CreatedAtUnix}` | Reusable until Read | Settings | Account (`MeId`) | Yes | Yes when `MeId` is a stable non-secret id. No `MeId`: session-only (not written). |
| Chat | `ITalk` unread threads | `chat:{threadId}:{LastAt unix}` | Reusable conversation | Social → messages | Character (active content id hex) | Yes from unread | Yes until `LastAt` changes. Unread 0 drops the row **without** a fingerprint. |
| People | none (`KindOf` / seed only) | — | — | Social → people | Character if ever produced | No producer | Session-only |
| Calendar | `CalendarApplet` `FireDue` | `cal:{itemId}:{LastFired}` | Event occurrence (`yyyyMMddHHmm`) | calendar applet | Character | Same occurrence will not re-fire (`LastFired`) | Yes; occurrences are distinct |
| Venue | `FireDue` `vn:` / `vs:` | `venue:{venueId}:{LastFired}` | Event occurrence | venues applet | Character | Same | Yes |
| Music live | `MusicApplet` follow-live | `music:{stationId}:{unix}` | Event | music applet | Account when `MeId` present | Live seed does not re-post on reload | Persist when Account owner is valid; next live is a new unix |
| Lifestream | `MusicApplet.Streams` | `music:lifestream:{unix}` | Applet / system event | music | Device | Session-unique unix | Device fingerprint; no personal id |
| VYBE | none | — | — | — | Account if ever | No producer | Session-only |
| Profile / applet / system | none beyond lifestream | — | — | — | Device | — | Session-only unless a stable non-content id exists |

Fingerprints never include message text. A new chat line changes `LastAt`, so it is not
the dismissed fingerprint. The same announcement after reload keeps the same
`id` + `CreatedAtUnix`. Calendar reminder occurrences use `LastFired`, not wall-clock at
post.

## File schema

```json
{
  "SchemaVersion": 1,
  "Rows": [
    {
      "Fingerprint": "chat:thread-id:1710000000",
      "Scope": "Character",
      "OwnerKey": "004000174b9c48a5",
      "DismissedAt": 1710000100
    }
  ]
}
```

| Field | Rule |
| --- | --- |
| `SchemaVersion` | `1` |
| `Fingerprint` | Compact id + generation. No payload / token / display names. |
| `Scope` | `Character`, `Account`, or `Device` |
| `OwnerKey` | Character: 16-char lowercase hex. Account: existing `MeId`. Device: empty. |
| `DismissedAt` | UTC unix seconds |

Cap **200 rows per (scope, owner)**. Prune oldest `DismissedAt` on write, not every frame.

## Rebuild and dismiss

On load / refresh, rebuild announcements, unread staff, and unread PearlChat threads.
Skip an exact fingerprint + scope + owner match. If generation changed, the old row does
not match and the notice returns.

Explicit swipe / OK / banner dismiss persists when the owner is valid. Opening a thread
still uses existing consume (`MarkRead`). If unread is already 0, the row is pruned and
**no** fingerprint is stored. Dismiss never marks unrelated threads read.

Character switch: drop Character-scope tray rows and rebuild; Device rows stay. Account
sign-in/out: drop Account-scope tray rows for the previous `MeId`; **do not** delete other
owners on disk.

Do not persist Character rows without a valid content id. Do not use session tokens as
`OwnerKey`.

## Mini wake (unchanged, session-only)

`pendingPocketNoticeId` is RAM only. Loading `notices.json` must never wake the mini.
`NoticeLaunch` still runs after `!wantFold && fold <= 0.04f`. The shade slider is not a
notice. Grab vs chip is unchanged. Dismiss-during-wake still clears pending.
