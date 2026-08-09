# Ideas

Shaders and effects worth building, with enough of a plan that picking one up later
doesn't mean starting the thinking from scratch. Nothing here is a promise, and the
order means nothing.

---

## Drawable surface: markers and spray cans

A pane you can draw on with a marker, tag with a spray can, and rub clean again, the way
the glass in Half-Life: Alyx works, plus paint.

**Where it starts.** `Wipeable` already does most of this. It owns a mask in UV space,
paints brush dabs into it with a `Hidden/Gallery/WipeBrush` material, and hands it to the
shader through a property block. Drawing is the same machinery with a colour: one RGBA
target instead of one channel, and more than one brush.

**The brushes.** Each is the same dab with a different profile, so they can share a
material and differ by a pass or a couple of uniforms.

| | Look | Behaviour |
|---|---|---|
| Marker | hard edge, even width, translucent ink that darkens where a stroke crosses itself | ink caps out quickly, so going over twice barely changes it |
| Spray | soft falloff, speckled, denser at the centre | builds while the button is held, so dwelling in one spot saturates and then runs |
| Rag | no colour, takes it away | smears rather than lifting cleanly, like the wipe already does |

**Hard parts.**

- Strokes have to be drawn as segments between mouse samples, not as separate dabs. The
  current wipe dabs per event and gets away with it because grime is blobby; a marker line
  drawn that way comes out as beads whenever the drag is quick.
- Texel density. A wipe is forgiving, marker ink is not: a 512 mask that looks fine as
  smeared dirt reads as a pixelated line. Either the mask scales with the surface area or
  the brush is drawn as a signed distance so the edge stays crisp at any resolution.
- The seam. `Wipeable` already redraws a dab on the far side of the u wrap; a segment
  crossing the seam has to be split, which is fiddlier.
- Ink on glass should tint what is behind it, spray paint should cover it. That is one
  more channel of intent than "colour and alpha", or two layers.

**Bonus.** Drips from an over-sprayed spot, which is the droplets simulation again with
the source picked by where the can was held.

---

## A cigarette that actually smokes

Not a prop with a glow: something that catches when lit, burns down, carries a column of
ash that eventually falls, and burns faster while it is being drawn on.

**The trick that makes it work.** Don't move geometry. Model the whole cigarette at full
length and let two numbers along its axis say what each part is:

- `_BurnAt` — where the ember is, 0 at the tip, 1 at the filter.
- `_AshFrom` — where the ash begins.

Everything before `_AshFrom` has already fallen away and is clipped. Between `_AshFrom`
and `_BurnAt` is ash. At `_BurnAt` is the ember. Past it is paper, yellowing as the heat
gets close. Knocking the ash off is then just `_AshFrom = _BurnAt - a little`, plus a
puff of ash particles for the piece that left. No mesh swapping, no seams.

**The ember.** A thin band at `_BurnAt`, HDR emissive so it blooms, hottest at the paper
edge and dying back into the ash. It should flicker on its own, and the flicker should be
noise along the ring rather than a global pulse, or it reads as a blinking light. Drawing
on it widens the band, pushes the colour from red towards orange-white, and raises the
burn rate for a second or two afterwards.

**The ash.** Grey, cracked, and it has to look fragile: slightly narrower than the paper,
with the cracks running around the axis rather than along it. The broken end wants a
noise clip so it looks snapped, not sliced.

**The parts that aren't the shader.**

- Burn rate: slow while it sits, faster on a draw, and it should keep creeping for a
  moment after — a cigarette does not stop glowing when you stop pulling.
- Ash falls on a tap, and also on its own, with the chance rising as the column gets
  longer. Both paths run the same code.
- Smoke is two different things and they are usually confused: the thin thread rising off
  the tip on its own, which is slow and breaks into turbulence about a hand's width up,
  and the exhaled cloud, which is fat, fast and short-lived. The thread is the one that
  sells it.
- Lighting it needs a moment where the tip flares wider and brighter than any drag, then
  settles.

**Controls it should have.** Burn rate, ash colour and how far it goes grey, ember colour
and brightness, how long a drag lasts, ash length before it gets fragile, smoke amount.

**Hard part.** The tip is the one place where the ember, the ash and the smoke source all
meet, and each of them wants to own it. Getting the ember to read as sitting *inside* a
tube of ash, rather than painted on the end of it, is most of the work.
