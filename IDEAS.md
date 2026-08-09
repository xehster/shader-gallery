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

---

## Cracked glass, by the slider

Glass that goes from a single hairline to a shattered mess on one control.

**Two different kinds of damage, and they should be separate layers.**

- **An impact.** Spokes running out from a point, crossed by rings, densest at the hit and
  petering out. It needs a point to run from, and `ForceField` already has the convention
  for that in this repo: a `_Hits` array of object-space positions with the age of each in
  `w`. Declaring `_AcceptsImpacts` would hand it the gallery's Impacts checkbox for free,
  and age in `w` means a crack can be made to spread over the moment after it lands.
- **Crazing.** A network with no centre, the way old glass goes. Voronoi borders, warped by
  noise so the cells aren't the tidy lizard-skin shape everybody recognises as a Voronoi.

**Making the slider add cracks rather than darken them.** Same trick as the dirt: a
threshold over a field, not a fade. Fading cracks in makes them look pencilled on. Give
every crack a rank — coarse network first, finer ones layered under it — and let the
slider decide how many ranks are admitted. At the bottom there is one line, further up it
branches, at the top the whole pane is a web.

**The thing that actually sells it.** Not the lines: the fragments. Give each cell a small
random tilt and bend the refraction by it, so what's behind the glass jumps a little from
one shard to the next. Cracks alone read as a decal; a view that breaks up across the
pieces reads as broken glass. This is cheap — it is the per-cell random the Voronoi
already gives, reused as a normal offset.

**Hard parts.**

- Cracks are thin, and thin lines alias into a shimmering mess as the camera pulls back.
  The droplets shader has this argued out already, including the relative-thickness
  toggle; whatever it landed on should be lifted rather than rethought.
- Real cracks are straighter than a Voronoi border, they branch at sharp angles, and they
  *stop*. Warping the domain helps some. Terminating a crack partway is the awkward one.
- Glass has thickness, and a crack lives inside it. Sampling the crack field a second time
  at a view-dependent offset gives the line a body instead of a surface scratch — worth
  trying early, it's a couple of lines and it changes everything.
- Object space or UV, same choice as the dirt. A solid wants object space; a pane wants UV
  so the cracks run through the thickness in a sensible direction.

**Bonus.** Past some amount, pieces should be *gone*: clip the odd cell out entirely, with
a brighter, thicker rim on the hole so it doesn't look like it was cut with scissors. And
placing cracks by clicking, which is `Wipeable` with a different brush.

---

## Bullet holes and melee marks

Hits that leave something behind: a punched hole with cracks running off it where a round
went through, a broad dent where something heavy landed. Glass first, but not only glass.

**This wants to share code with the cracked glass above, or be the same shader.** The
impact half of that idea — spokes from a point, rings crossing them — is exactly what a
bullet leaves. Writing the crack machinery twice would be daft. One way to think about it:
the cracked-glass slider is the ambient damage of the whole pane, and hits are local
sources dropped into the same field.

**Getting the hits in.** `ForceField`'s convention again: an array of object-space
positions with age in `w`, pushed through a property block, and `_AcceptsImpacts` so the
gallery's Impacts checkbox peppers it on its own for a demo. Two differences from the
shield:

- Four slots isn't enough. A wall gets shot a lot. Something like `Droplets`' 32, as a
  ring buffer where the oldest slot is recycled.
- The seed for each mark's shape has to be hashed **from the hit position**, not from the
  slot index. Recycle a slot with an index-based seed and an old mark on screen silently
  changes shape.

If a scene ever needs hundreds of marks, the escape hatch is `Wipeable`'s trick: stamp
settled marks into a mask and free the slot. The array keeps a mark alive and animatable,
the mask holds unlimited ones but freezes them. A hybrid — live in the array while it is
still spreading, stamped once it settles — is the honest answer and also the most work.

**Two marks, and they should not look like each other's cousins.**

| | Glass | Anything else |
|---|---|---|
| Bullet | a real hole, alpha-clipped, ring of pulverised white around it, radial cracks and a couple of rings | a dark pit with a bright rim of fresh material, dust spray, no cracks |
| Melee | no hole: a wide dent with a dense web, irregular outline | a scuff, elongated along the swing |

So the glass switch is not the cosmetic one it is in `DirtyGlass` — it changes what a mark
*is*. Better as a mode than a checkbox.

**Randomness.** One slider, 0 to 1. At 0 every mark is the same stamp, which is the right
look for something stylised; at 1 no two are alike. What it moves: how many spokes and how
far they run, the angle jitter between them, how round the hole is (the superellipse
exponent `Droplets` uses is good for this), rim thickness, overall size. All of it off the
one position hash, so nothing extra has to be stored per hit.

**Lifetime.** Age is already in `w`, so a lifetime slider costs almost nothing, with 0
meaning forever — worth having, because glass does not heal and often the mark should just
stay. When it does fade it should **retract**, shrinking back into its densest core and
going, the same threshold move as the dirt and the droplets, not a uniform fade to
transparent. And the other end matters more than the fade: a mark wants a birth, the
cracks racing outward over the first tenth of a second and a flash of dust at the point.

**Hard parts.**

- A stable frame on the surface to orient the mark in. UV tangents where there are any;
  built from the normal otherwise, which spins as the surface curves.
- Distance from the hit is straight-line distance, which is right on a pane and wrong on
  anything strongly curved, where a mark will wrap oddly. Fine for the first pass, worth
  writing down as a known limit.
- Cost. Thirty-two hits evaluated per pixel is real; cull each one by distance before
  doing any of the shape work.

**What comes free.** Two holes close together merge their crack fields into one, because
it is all one field summed per fragment rather than a stack of decals. That alone is a
good enough reason to do it this way instead of with decal projectors.

---

## Minimap

**Where the work actually is.** A second camera pointed down, rendering to a texture, is
the boring part and it is not a shader. The shader is the presentation, and that is where
a minimap gets its whole character: the mask it is cut to, how it rotates, and its style.
Worth being clear about that split before starting, or it turns into a camera-rigging
exercise with no shader in it.

**Fog of war is `Wipeable` again, inverted.** A mask in map space, painted where the
player has been, brush dabs following them around. That would be the third thing built on
the same paint-into-a-mask kit, after wiping dirt off and drawing on glass — good sign the
kit should be pulled out and named rather than copied a third time. Un-fogging wants a
soft brush and no going back; the wipe already behaves exactly like that.

**Things that are the shader's job.**

- The mask: circle, hex, or torn parchment, with a feathered edge rather than a hard cut.
- Rotation. Either the map turns under a fixed arrow or the arrow turns on a fixed map.
  Whichever it is, the *icons must not turn with it*, which means they cannot be baked
  into the map texture.
- Markers pinned to the rim when their target is off the map, squashing against the edge
  as they arrive there.
- Height. In anything with two floors, showing both at once is unreadable; slice by the
  player's height, either by clipping the camera or by tinting with height.
- Style, which is most of the fun: radar sweep with phosphor that decays behind it, ink on
  paper, blueprint. The sweep needs a per-pixel age, which is another small mask.

**Hard parts.**

- Everything on a minimap is thin, and it is drawn small. Line widths have to be in screen
  pixels, not world units — the same argument the droplets outline settled, and the third
  time it has come up now.
- The rim is where it all falls apart: the mask edge, the clamped markers and the border
  decoration all want the same few pixels.
- It is UI, so it needs the UI-Default plumbing that `GradientMap` and `PaletteSwapUI`
  already carry. Copy one of those as the starting point rather than a mesh shader.
