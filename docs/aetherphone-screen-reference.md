# Reference notes: how Aetherphone renders its screen

Read-only research, not implementation. This documents the *techniques* Aetherphone's window/
chassis/wallpaper system uses, in my own words, as a reference for planning Linkpearl's own
independent implementation of the same capabilities — none of this code was copied, ported, or
translated; see the project's standing "no code from Aetherphone" rule. Where a technique is
worth adopting, it gets re-designed and re-authored from scratch, the same way `ChassisGeometry`/
`ResizeGrip`/etc. already were.

Source: `FFXIV-Aetherphone-dev` ZIP, `src/Aetherphone/Windows/` and `src/Aetherphone/Core/`.

## What Linkpearl already has an equivalent of

- Chassis geometry (body → glass → screen nested rects, corner radii) — `ChassisGeometry`/
  `ChassisMetrics` already cover this, independently designed.
- A borderless, background-less ImGui window drawn as a physical object — `HandsetWindow` already
  does this (`WindowPadding`/`WindowBorderSize` zeroed, `WindowBg` cleared).
- Size steps — `HandsetSizeCatalog`, now shared between corner-drag and an explicit control.

## Techniques Linkpearl doesn't have yet, worth designing independently

**Wallpaper as a textured, aspect-fit image, not a flat color.** Their approach: a wallpaper
library resolves an id to a file path, loads it async via the platform's texture provider into a
cache keyed by path (tracking ready/loading/failed state separately so a still-loading image
doesn't retry every frame), computes a crop rect from the source image's size against the
screen's target aspect ratio (a "cover" fit — fill the frame, crop the overflow), and draws it as
a plain textured quad, `PushClipRect`'d only when the drawn quad is larger than the visible
shape (e.g. during a zoom/parallax animation). A flat fallback color covers the gap before the
real image is ready. This is the same shape `ITextureSource` in Linkpearl's own Abstractions
already anticipates — nothing here requires new architecture, just an actual implementation
behind it (currently `ITextureSource` has no concrete implementation at all).

**Day/night and light/dark wallpaper variants, blended, not swapped.** Two wallpaper ids are
tracked per theme (a "light" and a "dark" variant); both draw every frame, the dark one layered
on top at an alpha equal to a `ThemeDarkness` value that's spring-smoothed toward a target (0 for
forced-light, 1 for forced-dark, or the current auto day/night blend for "auto" mode, itself
spring-smoothed across a sunrise/sunset hour window). The effect is a slow cross-fade rather than
a hard cut when day turns to night or the player changes the theme setting.

**Wallpaper-driven legibility, not a fixed scrim.** Before drawing UI text over a wallpaper, they
sample the wallpaper image's average brightness (a small fixed-size downsample, not the full
image) and use that to decide how dark a scrim to lay over it — a busy/bright wallpaper gets a
stronger dim, a calm/dark one barely any. This is worth adopting as a *design decision*, not
necessarily the exact sampling technique: whatever wallpaper system Linkpearl builds should dim
proportionally to what the wallpaper actually needs for text contrast, not a single hardcoded
opacity that looks right on one wallpaper and wrong on the next.

**Beveled edge lighting on the chassis frame, not flat color.** A thin bright stroke along the
top/left chassis edges and a dim stroke along bottom/right (plus matching per-corner gradient
strokes) fakes a raised, lit bezel — a cheap, purely 2D technique (a handful of `AddLine`/
corner-stroke calls at a computed inset), not a real lighting model. Linkpearl's chassis is
currently flat-shaded everywhere; this is a candidate technique for giving the AQUOS-style frame
some dimensionality without needing texture assets, if that's ever wanted — separate from the
"real photographed-metal case art" question, which does need actual image assets.

**Case art as a texture, with cross-fade between skins.** Their "Art" case kind draws an actual
image over the chassis body instead of a flat color, still layering the glass/screen fills and
edge treatments on top so the screen area and bezel seam read correctly regardless of the
underlying art. Switching case skins cross-fades the old and new art via a simple linear progress
value rather than cutting instantly. Confirms the plan already in Linkpearl's `STATUS.md`: the
ornate hardware-reference look needs real front-bezel/back-plate image assets rendered as a
texture, and a cross-fade is the obvious way to make switching between finishes feel intentional
rather than jarring.

**Landscape as a continuous blend, not a discrete flip.** Rotating the phone doesn't snap between
portrait and landscape sizes; a blend factor eases toward 0 or 1 over a fixed duration and the
window size is `Lerp`'d between the portrait and landscape (transposed) dimensions using an
eased curve, with every chassis-drawing routine carrying a landscape/portrait branch (button
placement, edge-cap orientation) driven off the same blend. Linkpearl has no landscape mode at
all currently — worth deciding deliberately (the design brief hasn't mentioned it) rather than
never revisiting.

**Minimize/dock as a small state machine plus a spring, not a boolean.** Four states (idle,
collapsing, docked, expanding) with a critically-damped spring driving progress toward 0 or 1,
further reshaped by an ease-in-out curve before use — two layers of easing (physical spring
response, then a deliberate shape on top) rather than one. The docked size is a small fixed
pixel footprint, not a scaled-down version of the full phone. Linkpearl's own STATUS.md already
lists "minimized device faces" as a documented, undesigned gap; this confirms the state-machine
shape worth reimplementing independently when that work starts.

**Screen-corner masking as a cleanup pass, not a clipping constraint.** Rather than fighting to
clip arbitrary content (video, wallpaper) to a rounded rect, they draw the content as a plain
rect and then paint the four *outside* corners back over in the frame color afterward. Simpler
than clip-region gymnastics for content that doesn't otherwise respect rounding. Linkpearl's own
`IPaintSurface` already supports arbitrary-rect clipping for regular content (used for the scroll
viewport), so this specific technique only becomes relevant if/when something is drawn as a plain
unclippable rect that needs to look rounded after the fact.

## Sizing & scale system — two different approaches upstream, worth choosing deliberately

This one matters for the corner-drag/size-stepper work already built, so it's worth being
precise about what upstream actually does versus what the old Linkpearl fork did versus what
Linkpearl's rebuild currently does — three different designs, not one lineage.

**Aetherphone (upstream) — continuous width, soft-snap only near presets.** A single float
`width`, free to be anything from 240 to 900, clamped but not stepped. Height is derived from a
fixed aspect ratio (`DesignHeight / DesignWidth`, not width/height pairs). The zoom factor used
for fonts/UI scale is the plainest possible relationship: `width / DesignWidth` — resizing to
exactly double the design width means exactly double zoom, no lookup table. Six preset widths
exist, but `Snap()` only pulls the value to one of them when already within 6 pixels — drag to
anywhere else and it stays exactly there. The result: dragging feels like resizing a real window
(smooth, continuous, any value reachable), with presets acting as gentle magnets rather than the
only reachable stops.

**Old Linkpearl fork — six discrete steps, always snapped, no continuous option.** (This is what
I read earlier this session, not upstream Aetherphone.) A fixed array of six scale multipliers
(0.778 through 1.389, labeled XS through XXL) applied to a 450×800 base size. `Snap()` always
returns the *nearest* of the six values — there is no "close enough, leave it alone" case; every
width the resize drag produces gets pulled to one of exactly six sizes. Two base sizes exist
(Phone 450×800, Tablet 600×800) rather than one aspect ratio applied continuously.

**Linkpearl's rebuild (current) — copied the fork's discrete-step design, not upstream's.**
`HandsetSizeCatalog` (now in `Linkpearl.Abstractions/Chassis`) uses the same six-named-step,
always-snapped shape as the old fork: `ScaleSteps`/`StepLabels` arrays, `SnapToStep` always
returns the closest of six values, no continuous in-between state ever persists. `ResizeGrip`
mid-drag *does* scale continuously (the live value while dragging isn't stepped), but releasing
snaps to the nearest of six — so today's behavior is "continuous while held, discrete on
release," a middle ground between the two upstream designs rather than a copy of either.

**What's worth deciding, not just noting:** whether Linkpearl should move toward upstream's
"continuous with soft presets" (more flexible, feels more like a real resizable window, but no
fixed vocabulary of sizes to refer to in copy like "M" or "L") or keep the current "discrete named
steps" model (simpler to reason about and to expose as the You-tab stepper control, but a harder
snap on every release). Nothing about the existing `ResizeGrip`/`HandsetSizeCatalog` split forces
either choice — swapping the snap behavior is a change to `HandsetSizeCatalog.SnapToStep` and
`ResizeGrip`'s release logic, not a structural rewrite, so this can be revisited without disruption
whenever there's a preference.

## Not investigated

Update-check polling, position persistence/locking, the drag-gesture-surface interaction that
suppresses window movement during in-app drags, and the dynamic-island-style activity chrome
were seen in passing (`PhoneWindow.cs`) but not read in depth — flagged here so a future pass
knows they exist without re-discovering them from scratch.
