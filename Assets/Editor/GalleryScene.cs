using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Everything that changes the scene itself: put a subject in, take one out,
/// lay the shelves out again. Kept apart from the list window, which only deals
/// with the list and its checkboxes.
/// </summary>
public static class GalleryScene
{
    public const string MaterialsFolder = "Assets/Materials/Gallery";

    const float Step = 3f;
    const float SphereY = 1f;
    const float SphereScale = 2f;
    const float PanelPixels = 200f; // canvas units before scaling down to metres

    public const int MaxPerShelf = 10;

    const float ShelfThickness = 0.3f;
    const float ShelfDepth = 3.4f;
    const float FirstShelfTop = 1.2f;
    const float LabelStandoff = 0.03f; // clear of the shelf face so the text doesn't z-fight

    public static GalleryRig FindRig()
    {
        return Object.FindFirstObjectByType<GalleryRig>();
    }

    /// <summary>
    /// Reuse a gallery material on this shader if there is one, otherwise make it.
    /// The name is checked first: several materials can share a shader, and grabbing
    /// whichever one turns up first lands you with the backdrop's material.
    /// </summary>
    public static Material MaterialFor(Shader shader)
    {
        string wanted = MaterialsFolder + "/M_" + ShortName(shader).Replace(" ", "") + ".mat";

        var byName = AssetDatabase.LoadAssetAtPath<Material>(wanted);
        if (byName != null && byName.shader == shader) return byName;

        if (byName == null)
        {
            var created = new Material(shader);
            AssetDatabase.CreateAsset(created, wanted);
            return created;
        }

        // name is taken by a material on a different shader, so fall back to a fresh one
        var spare = new Material(shader);
        AssetDatabase.CreateAsset(spare, AssetDatabase.GenerateUniqueAssetPath(wanted));
        return spare;
    }

    public static string ShortName(Shader shader)
    {
        string n = shader.name;
        int slash = n.LastIndexOf('/');
        return slash >= 0 ? n.Substring(slash + 1) : n;
    }

    /// <summary>
    /// A sphere shows most shaders off best, but some need flat faces to say anything:
    /// anything projecting a grid or looking through the surface has nothing square to
    /// work with on a ball. Those declare a hidden _WantsCube and get one.
    /// </summary>
    public static bool WantsCube(Material material)
    {
        return material != null && material.HasProperty("_WantsCube");
    }

    public static void AddShader(GalleryRig rig, Shader shader)
    {
        var root = SpheresRoot();
        var mat = MaterialFor(shader);
        string label = ShortName(shader);

        bool cube = WantsCube(mat);
        var go = GameObject.CreatePrimitive(cube ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = (cube ? "Cube_" : "Sphere_") + label.Replace(" ", "");
        go.transform.SetParent(root, true);

        // A cube of the same width reads much bigger than a sphere, because the corners
        // reach further than the radius. Trim it so the two take up the same room.
        float size = cube ? SphereScale * 0.8f : SphereScale;
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Undo.RegisterCreatedObjectUndo(go, "Add to gallery");

        Append(rig, new GalleryRig.Subject
        {
            label = label,
            target = go.transform,
            labelObject = MakeLabel(root, label),
            animate = true,
            // half its height above the shelf, so whatever the shape it stands on the wood
            restPosition = new Vector3(0f, size * 0.5f, 0f),
            restScale = go.transform.localScale,
            restRotation = go.transform.rotation
        });
    }

    /// <summary>
    /// UI shaders expect the things a Canvas hands them: the GUI z-test mode, a clip
    /// rect, vertex colours. Give them a world-space Canvas rather than a quad, or the
    /// next one you drop in draws garbage. The picture they recolour is the sample
    /// texture on the rig, which is a live render of a reference sphere.
    /// </summary>
    public static void AddCanvas(GalleryRig rig, Shader shader)
    {
        var root = SpheresRoot();
        var mat = MaterialFor(shader);
        string label = ShortName(shader);

        var go = new GameObject("Panel_" + label.Replace(" ", ""), typeof(Canvas), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.RawImage));
        go.transform.SetParent(root, true);
        Undo.RegisterCreatedObjectUndo(go, "Add to gallery");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(PanelPixels, PanelPixels);
        rect.localScale = Vector3.one * (SphereScale / PanelPixels);
        rect.localRotation = Quaternion.identity;

        var image = go.GetComponent<UnityEngine.UI.RawImage>();
        image.material = mat;
        image.texture = rig.sampleTexture;

        Append(rig, new GalleryRig.Subject
        {
            label = label,
            target = go.transform,
            labelObject = MakeLabel(root, label),
            animate = true,
            flat = true,
            restPosition = new Vector3(0f, SphereY, 0f),
            restScale = rect.localScale,
            restRotation = Quaternion.identity
        });
    }

    /// <summary>UI shaders carry the stencil and colour-mask properties of UI-Default.</summary>
    public static bool IsUIShader(Shader shader)
    {
        var probe = new Material(shader);
        bool ui = probe.HasProperty("_StencilComp") && probe.HasProperty("_ColorMask");
        Object.DestroyImmediate(probe);
        return ui;
    }

    public static void AddParticles(GalleryRig rig, GameObject prefab)
    {
        var root = SpheresRoot();
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        Undo.RegisterCreatedObjectUndo(inst, "Add to gallery");

        Append(rig, new GalleryRig.Subject
        {
            label = prefab.name,
            target = inst.transform,
            labelObject = MakeLabel(root, prefab.name),
            animate = false, // a bounce with squash turns fire into a jellyfish
            restPosition = new Vector3(0f, 0.2f, 0f),
            restScale = inst.transform.localScale,
            restRotation = inst.transform.rotation
        });
    }

    public static void Remove(GalleryRig rig, int index)
    {
        if (index < 0 || index >= rig.subjects.Length) return;

        var s = rig.subjects[index];
        if (s.labelObject != null) Undo.DestroyObjectImmediate(s.labelObject);
        if (s.target != null) Undo.DestroyObjectImmediate(s.target.gameObject);

        var list = new List<GalleryRig.Subject>(rig.subjects);
        list.RemoveAt(index);

        Undo.RecordObject(rig, "Remove from gallery");
        rig.subjects = list.ToArray();
    }

    /// <summary>Drop one material slot, leaving the subject and its other materials alone.</summary>
    public static void RemoveMaterialSlot(GalleryRig rig, int index, int slot)
    {
        if (index < 0 || index >= rig.subjects.Length) return;

        var target = rig.subjects[index].target;
        var renderer = target != null ? target.GetComponent<MeshRenderer>() : null;
        if (renderer == null) return;

        var slots = new List<Material>(renderer.sharedMaterials);
        if (slot < 0 || slot >= slots.Count || slots.Count < 2) return;

        slots.RemoveAt(slot);
        Undo.RecordObject(renderer, "Remove material from gallery");
        renderer.sharedMaterials = slots.ToArray();

        // the label named the pair, so drop the part that just left
        var s = rig.subjects[index];
        int plus = s.label.IndexOf(" + ", System.StringComparison.Ordinal);
        if (plus > 0)
        {
            Undo.RecordObject(rig, "Remove material from gallery");
            s.label = s.label.Substring(0, plus);
        }
    }

    /// <summary>
    /// Deal the subjects onto shelves, build the shelves under them, then fit the
    /// floor, backdrop and wide shot around the whole thing.
    /// </summary>
    public static void Layout(GalleryRig rig)
    {
        int n = rig.subjects.Length;
        if (n == 0) return;

        Undo.RecordObject(rig, "Lay out gallery");

        int columns = ColumnsFor(rig);
        int shelves = Mathf.CeilToInt(n / (float)columns);
        float spacing = Mathf.Max(3f, rig.shelfSpacing);
        float width = (columns - 1) * Step;

        for (int i = 0; i < n; i++)
        {
            var s = rig.subjects[i];
            if (s == null || s.target == null) continue;

            int shelf = i / columns;
            int column = i % columns;

            // last shelf can be short, so centre whatever is actually on it
            int onThisShelf = Mathf.Min(columns, n - shelf * columns);
            float rowStart = -(onThisShelf - 1) * Step * 0.5f;

            float x = rowStart + column * Step;
            float top = ShelfTop(shelf, spacing);
            float y = top + (s.animate ? SphereY : 0.2f);

            s.restPosition = new Vector3(x, y, 0f);
            s.target.position = s.restPosition;

            // never read the live transform back: a bounce squashes it, and laying out
            // mid-hop used to bake that squash in as the subject's resting shape
            if (s.restScale == Vector3.zero) s.restScale = s.target.localScale;
            s.target.localScale = s.restScale;
            s.target.rotation = s.restRotation;

            if (s.labelObject == null) continue;
            s.labelObject.name = "Label_" + i;
            PlaceLabel(s.labelObject, x, top, s.label);
        }

        BuildShelves(rig, shelves, width, spacing);

        // stop the hop before it headbutts the shelf above
        float headroom = spacing - SphereScale - 0.6f;
        if (shelves > 1 && rig.jumpHeight > headroom) rig.jumpHeight = Mathf.Max(1f, headroom);

        float topShelf = ShelfTop(shelves - 1, spacing);
        float stackHeight = topShelf + rig.jumpHeight + SphereScale;

        var floor = GameObject.Find("Floor (ConcreteTriplanar)");
        if (floor != null)
        {
            floor.transform.position = new Vector3(0f, floor.transform.position.y, 0f);
            floor.transform.localScale = new Vector3(width + 20f, 0.5f, 14f);
        }

        var backdrop = GameObject.Find("Backdrop");
        if (backdrop != null)
        {
            backdrop.transform.position = new Vector3(0f, stackHeight * 0.5f + 2f, 6f);
            backdrop.transform.localScale = new Vector3(width + 28f, stackHeight + 14f, 0.5f);
        }

        var cam = rig.galleryCamera != null ? rig.galleryCamera : Camera.main;
        if (cam != null)
        {
            // fit both ways: the row can be wider than it is tall, or the other way round
            float halfW = width * 0.5f + 2.6f;
            float halfH = stackHeight * 0.5f + 1.2f;

            float vFovHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hFovHalf = Mathf.Atan(Mathf.Tan(vFovHalf) * cam.aspect);
            float dist = Mathf.Max(halfW / Mathf.Tan(hFovHalf), halfH / Mathf.Tan(vFovHalf)) * 1.08f;

            cam.transform.position = new Vector3(0f, stackHeight * 0.45f, -dist);
            cam.transform.rotation = Quaternion.identity;
            rig.galleryCamera = cam;
            rig.SaveWideShot();
            rig.FocusWide();
        }

        EditorUtility.SetDirty(rig);
        MarkSceneDirty(rig);
    }

    /// <summary>
    /// How many subjects go on one shelf. Set it by hand on the rig, or leave it at 0
    /// and get as few shelves as ten-per-shelf allows, split evenly: 16 becomes 8+8
    /// rather than four short shelves, 20 becomes 10+10.
    /// </summary>
    public static int ColumnsFor(GalleryRig rig)
    {
        int n = Mathf.Max(1, rig.subjects.Length);
        if (rig.shelfColumns > 0) return Mathf.Clamp(rig.shelfColumns, 1, MaxPerShelf);

        int shelves = Mathf.CeilToInt(n / (float)MaxPerShelf);
        return Mathf.CeilToInt(n / (float)shelves);
    }

    static float ShelfTop(int index, float spacing)
    {
        return FirstShelfTop + index * spacing;
    }

    /// <summary>Rebuild the shelf slabs: as many as the subjects need, no leftovers.</summary>
    static void BuildShelves(GalleryRig rig, int count, float width, float spacing)
    {
        var root = GameObject.Find("Shelves");
        if (root == null)
        {
            root = new GameObject("Shelves");
            Undo.RegisterCreatedObjectUndo(root, "Lay out gallery");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_ConcreteFloor.mat");

        for (int i = root.transform.childCount - 1; i >= count; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

        for (int i = 0; i < count; i++)
        {
            GameObject shelf;
            if (i < root.transform.childCount)
            {
                shelf = root.transform.GetChild(i).gameObject;
            }
            else
            {
                shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.transform.SetParent(root.transform, true);
                if (mat != null) shelf.GetComponent<MeshRenderer>().sharedMaterial = mat;
                Undo.RegisterCreatedObjectUndo(shelf, "Lay out gallery");
            }

            shelf.name = "Shelf_" + i;
            shelf.transform.position = new Vector3(0f, ShelfTop(i, spacing) - ShelfThickness * 0.5f, 0f);
            shelf.transform.localScale = new Vector3(width + Step + 1f, ShelfThickness, ShelfDepth);
        }
    }

    /// <summary>Labels ride the front edge of the shelf, under their subject, like price cards.</summary>
    static void PlaceLabel(GameObject labelObject, float x, float shelfTop, string text)
    {
        labelObject.transform.position = new Vector3(x, shelfTop - ShelfThickness * 0.5f, -ShelfDepth * 0.5f - LabelStandoff);
        labelObject.transform.rotation = Quaternion.identity;

        var tmp = labelObject.GetComponent<TMPro.TextMeshPro>();
        if (tmp == null) return;

        tmp.text = text;
        tmp.color = Color.black;
    }

    public static void MarkSceneDirty(GalleryRig rig)
    {
        if (rig == null || Application.isPlaying) return;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        SceneView.RepaintAll();
    }

    static void Append(GalleryRig rig, GalleryRig.Subject subject)
    {
        Undo.RecordObject(rig, "Add to gallery");
        var list = new List<GalleryRig.Subject>(rig.subjects) { subject };
        rig.subjects = list.ToArray();
    }

    static Transform SpheresRoot()
    {
        var root = GameObject.Find("Spheres");
        if (root == null)
        {
            root = new GameObject("Spheres");
            Undo.RegisterCreatedObjectUndo(root, "Add to gallery");
        }
        return root.transform;
    }

    static GameObject MakeLabel(Transform parent, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, true);

        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        if (TMPro.TMP_Settings.defaultFontAsset != null) tmp.font = TMPro.TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = 2.6f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.rectTransform.sizeDelta = new Vector2(3.6f, 1.2f);

        Undo.RegisterCreatedObjectUndo(go, "Add to gallery");
        return go;
    }
}
