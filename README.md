# Shader Gallery

I kept writing shaders and then losing track of them — one lives in a game project, another in some test scene, a third only exists as a screenshot in a chat. So I made myself a shelf to put them on.

It's a Unity project with one scene in it: shelves, a sphere per shader, a label under each. Drop a shader into the project, tick it in a list, and it shows up on a shelf with everything else. Handy for comparing them side by side, and for recording a clip of one without building anything around it.

Unity 6000.0.32f1 · URP 17.0.3

Open `Assets/Scenes/ShaderGallery.unity` and select **Gallery Rig** — the whole panel is in the inspector.

## Putting a shader on a shelf

**Shader Gallery → Shader List** lists every shader in the project and every prefab with particles in it. Tick what you want to see, hit Apply.

A material gets made if there isn't one already, the sphere goes up, the shelves rearrange themselves, and the camera pulls back to fit. Untick to take something down. Shaders that already have a job elsewhere — the skybox, anything driving a renderer feature — say so in the list, since a sphere won't tell you much about those.

Shelves fill to ten and then start a new one, splitting evenly: eleven shaders sit as 6+5, twenty as 10+10. You can also just set the number per shelf yourself.

## What the panel does

**Spin / Bounce / Still** — how the row behaves. Bounce runs a wave with a springy second hop and squash on landing; spin turns everything in place; still is still. All of it animates in Edit Mode, so you can record without entering play. Particle systems get stepped by hand for the same reason.

**Vertex snap / Affine warp** — my shaders have a PS1 thing going on, and these two kill both artifacts across every material at once, which is the fastest way to see what a shader looks like underneath.

**Close-up** — a button per subject. The camera flies in and tracks the jump, and everything else hides so the frame is clean. **Labels** turns the text off for recording.

**Shader settings** — a foldout per material with that shader's own properties. Nothing is hard-coded, so whatever you add brings its settings along.

**Renderer features** — toggles for the full-screen passes on the URP renderer (dither, fog, and so on). These are project-wide, so put them back when you're done.

## Recording

Close-up, labels off, capture with ShareX or Unity Recorder, then:

```bash
ffmpeg -i clip.mp4 -vf "fps=15,scale=480:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" -loop 0 clip.gif
```

Don't turn on palette dithering if your shader already dithers — the file doubles in size and looks the same.

## Notes

The shaders in here came from my own projects; the public copies live in [unity-shaders](https://github.com/xehster/unity-shaders).

A few textures are placeholders I grabbed off the web so things render on import — the concrete maps in `Assets/Textures/Concrete/` and the two `placeholder_*` files next to the particle system. They're not mine to license, so swap them before publishing anything.
