# Shader Gallery

I write shaders and then lose them. One sits in a game project, another in some test scene, a third one I only have as a screenshot somewhere. So I made a place to keep them.

It's a Unity project with one scene: shelves, a sphere per shader, a label on the shelf edge. You drop a shader into the project, tick it in a list, and it shows up next to the others. Good for comparing them, and for recording a clip of one without building anything around it.

UI shaders go in here too. Those get a flat panel instead of a sphere, showing a live render of a reference sphere for them to work on.

![The gallery scene](docs/gallery.gif)

Unity 6000.0.32f1, URP 17.0.3.

Open `Assets/Scenes/ShaderGallery.unity` and select **Gallery Rig**. The panel is in the inspector.

## Putting a shader on a shelf

**Shader Gallery → Shader List** lists every shader in the project and every prefab with particles in it. Tick what you want, hit Apply.

A material is made if there isn't one yet, the sphere goes up, the shelves rearrange, the camera pulls back to fit. Untick to take something down.

Shaders that already have a job somewhere else say so in the list. The skybox, anything driving a renderer feature. A sphere won't tell you much about those.

Shelves hold ten and then a new one starts. The split is even, so eleven shaders sit as 6+5 and twenty as 10+10. You can also set the number per shelf yourself.

## 2D shaders

UI shaders don't belong on a sphere, so they get a flat panel instead. A camera off to the side renders a reference sphere into a render texture, and that picture is what the panel shows. The shader recolours it live, so the sphere keeps turning inside the panel.

The list marks a shader as 2D on its own if it carries the stencil and colour mask properties that everything based on UI-Default has. The little **3D / 2D** button next to the tick is there to overrule that when the guess is wrong.

Panels sit on a world space Canvas, not a quad. UI shaders expect a Canvas to hand them the GUI z-test mode, a clip rect and vertex colours, and without one the next shader you drop in can draw garbage.

Swap the picture for your own art in **Sample Texture** on the rig. For something like a gradient map, a drawing with real black linework shows the effect better than a checker.

## What the panel does

**Spin / Bounce / Still.** Bounce runs a wave with a small second hop and a squash on landing. Spin just turns everything in place. Still is still. All of it runs in Edit Mode, no need to hit play, and particle systems get stepped by hand for the same reason.


**Close-up.** A button per subject. The camera flies in and follows the jump, everything else hides. **Labels** turns off the text for recording.

**Shader settings.** A foldout per material with that shader's own properties, drawn by Unity's own material inspector. Nothing is hard-coded, so whatever you add brings its settings with it. The PS1 knobs on my shaders live here too, per material, rather than being driven from the panel.

**Renderer features.** Toggles for the full-screen passes on the URP renderer. These are project-wide, so put them back when you're done.

## Recording

Close-up, labels off, capture with ShareX or Unity Recorder, then:

```bash
ffmpeg -i clip.mp4 -vf "fps=15,scale=480:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" -loop 0 clip.gif
```

If your shader already dithers, leave palette dithering off. The file doubles in size and looks the same.

## Notes

The shaders came from my own projects. Public copies live in [unity-shaders](https://github.com/xehster/unity-shaders).

Some textures are placeholders I grabbed off the web so things render on import: the concrete maps in `Assets/Textures/Concrete/` and the two `placeholder_*` files next to the particle system. They aren't mine to license, so swap them before publishing anything.
