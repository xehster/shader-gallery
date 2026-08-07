# ShaderLab

A standalone Unity project for looking at shaders away from the game: one scene, a row of spheres, one per shader. Built so I can compare them side by side and record clips of each.

Unity 6000.0.32f1 · URP 17.0.3

Open `Assets/Scenes/ShaderLab.unity`, select **ShaderLab Rig** — the whole panel is in the inspector.

## What's in the scene

Shelves of spheres: PS1 Lit, PS1 Lit Chromatic, PS1 Lit Transparent, Hologram, PS1 Lit + MoveOutline, ConcreteTriplanar, PS1 Lit Emissive, Fire, Holographic, FireParticles. Plus `FireParticleSystem`, which is particles rather than a sphere and so doesn't bounce. Dark backdrop behind, concrete floor below, gradient skybox above.

Shelves are worked out from how many subjects there are: as few shelves as ten-per-shelf allows, with the subjects split evenly between them.

| Subjects | Layout |
|---|---|
| 10 | 10 |
| 11 | 6+5 |
| 16 | 8+8 |
| 20 | 10+10 |
| 24 | 8+8+8 |
| 40 | 10+10+10+10 |

The **Shelves** section of the panel has both as sliders: **Per shelf** (0 for the automatic split above, or a fixed count up to 10) and **Spacing** between shelves. Moving either rebuilds the scene straight away, and a line under them spells out the result — `11 subjects: 6+5`. **Rebuild** re-lays everything if something got dragged out of place by hand.

Jump height is capped to the spacing, so nothing headbutts the shelf above.

Labels are black and ride the front edge of the shelf under their sphere, like price cards — same treatment on every shelf, so nothing floats in mid-air and nothing is hidden by the edge it's lying behind.

Everything animates **in Edit Mode**, no need to hit play — clips can be recorded straight from the Game View. The rig steps the particle systems by hand too, since Unity leaves them frozen outside of play.

## The panel

At the top, the things that get touched constantly:

- **Spin / Bounce / Still** — how the row moves. Spin turns everything in place with no squash or stretch, Bounce runs the wave with its springy second hop, Still just puts everything back on its mark.
- **Vertex snap** and **Affine warp** — kill the PS1 artifacts across every lab material at once.
- **Close-up** — one button per subject, appearing as things are added. The camera flies in and tracks the jump; solo hides the rest of the row.

The rest folds away:

| Section | What's inside |
|---|---|
| Motion settings | jump height, cycle, phase offset, second hop, squash and stretch — or spin speed, depending on the mode |
| Camera | distance, height, jump tracking, solo, save the current view as the wide shot |
| Shader settings | a foldout per material in the row, each with that shader's own properties |
| Renderer features | RetroDither, HeightFog, ChromaFringes, SSAO — these are shared with the whole project |
| Subjects | the raw list |

Shader settings aren't hard-coded: the panel walks the materials actually sitting in the row and draws each one's properties, so a shader added through the Shader List brings its settings with it. The two PS1 sliders stay at the top of that section, since they apply to every material at once.

## Adding a shader

**ShaderLab → Shader List** (or the button in the panel) opens a window listing every shader in the project and every prefab with particles in it. A tick means it's in the scene.

1. Drop the `.shader` or `.shadergraph` anywhere under `Assets/`.
2. Hit **Rescan** — it turns up on its own.
3. Tick it, hit **Apply**.

The material is handled for you: if `Assets/Materials/ShaderLab/` already has one on that shader it gets reused, otherwise a new one is created. The sphere, its label, its close-up button and its place in the row all appear by themselves, and the row is laid out again around the centre with the floor, backdrop and wide shot refitted to the new length.

Unticking removes the subject and its label.

Particles go in as the prefab itself, marked `animate = false` — a bounce with squash turns fire into a jellyfish. Close-ups and solo still work on them; `Static Look Height` sets where the camera aims.

Shaders with no geometry pass — full-screen effects and the skybox — are flagged **"won't draw on its own"**. Nothing stops you ticking them, but there's little point: those belong on `Assets/Settings/PC_Renderer.asset` as renderer features, the way HeightFog and RetroDither already are. `MoveOutline` lands in the same bucket, since it draws as a second material slot over a normal one.

## Recording

Close-up → solo → **Labels** off, capture with whatever you like (ShareX, Unity Recorder), then:

```bash
ffmpeg -i clip.mp4 -vf "fps=15,scale=480:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" -loop 0 clip.gif
```

Leave the palette dithering off. RetroDither already sprays per-pixel noise on every frame, and a second layer of it only doubles the file size for no visible gain.

## Where this came from

The hand-written shaders and the scene came out of Purrfield; the Shader Graph ones and the particle system came from the `unity-shaders` repo, which is where the public copies live. This project is a bench — nothing here goes back into the game.

Placeholder textures, not mine to license, swap them before publishing anything:

- `Assets/Textures/Concrete/` — Damaged_Concrete_Wall, 1K, so the triplanar shader has something to show;
- `Assets/ParticleSystem/placeholder_noise.jpg` and `placeholder_fire_flipbook.png` — came along with the particles.
