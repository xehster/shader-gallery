using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lists everything that can go in the row: every shader in the project and
/// every prefab with particles in it. A tick means it's in the scene. The list is
/// rebuilt on demand, so a new shader only needs a Rescan to show up.
/// </summary>
public class ShaderList : EditorWindow
{
    class Row
    {
        public Object asset;
        public string name;
        public string note;      // where the shader already has a job, if anywhere
        public bool inScene;     // how things are
        public bool wanted;      // how the user wants them
        public int subjectIndex; // -1 when not in the scene
        public int slot;         // which material slot it sits in
        public bool asPanel;     // show on a flat canvas instead of a sphere
    }

    struct Placement
    {
        public readonly int Subject;
        public readonly int Slot;

        public Placement(int subject, int slot)
        {
            Subject = subject;
            Slot = slot;
        }
    }

    readonly List<Row> _shaders = new List<Row>();
    readonly List<Row> _particles = new List<Row>();
    Vector2 _scroll;
    bool _scanned;

    [MenuItem("Shader Gallery/Shader List")]
    public static void Open()
    {
        var w = GetWindow<ShaderList>();
        w.titleContent = new GUIContent("Shader List");
        w.minSize = new Vector2(380f, 320f);
        w.Scan();
    }

    void OnFocus()
    {
        if (_scanned) Scan();
    }

    void OnGUI()
    {
        var rig = GalleryScene.FindRig();
        if (rig == null)
        {
            EditorGUILayout.HelpBox("Open the gallery scene - this window works on the rig in it.", MessageType.Info);
            if (GUILayout.Button("Open scene"))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ShaderGallery.unity");
                Scan();
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rescan")) Scan();
            using (new EditorGUI.DisabledScope(!HasChanges()))
                if (GUILayout.Button("Apply", GUILayout.Width(110f))) Apply(rig);
        }

        EditorGUILayout.Space(4f);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Shaders", _shaders);
        EditorGUILayout.Space(8f);
        DrawSection("Particles", _particles);

        EditorGUILayout.EndScrollView();

        int add = 0, remove = 0;
        CountChanges(ref add, ref remove);
        if (add > 0 || remove > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Adding " + add + ", removing " + remove, EditorStyles.miniLabel);
        }
    }

    void DrawSection(string title, List<Row> rows)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (rows.Count == 0)
        {
            EditorGUILayout.LabelField("  nothing found", EditorStyles.miniLabel);
            return;
        }

        foreach (var row in rows)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                row.wanted = EditorGUILayout.ToggleLeft(row.name, row.wanted, GUILayout.MinWidth(160f));

                using (new EditorGUI.DisabledScope(row.inScene))
                    row.asPanel = GUILayout.Toggle(row.asPanel, row.asPanel ? "2D" : "3D",
                        EditorStyles.miniButton, GUILayout.Width(30f));

                if (!string.IsNullOrEmpty(row.note))
                    EditorGUILayout.LabelField(row.note, EditorStyles.miniLabel, GUILayout.Width(120f));

                if (GUILayout.Button("→", EditorStyles.miniButton, GUILayout.Width(22f)))
                    EditorGUIUtility.PingObject(row.asset);
            }
        }
    }

    /// <summary>Rebuild both lists from the project and check them against the scene.</summary>
    void Scan()
    {
        _shaders.Clear();
        _particles.Clear();
        _scanned = true;

        var rig = GalleryScene.FindRig();
        var shadersInScene = new Dictionary<Shader, Placement>();
        var prefabsInScene = new Dictionary<GameObject, int>();

        if (rig != null)
        {
            for (int i = 0; i < rig.subjects.Length; i++)
            {
                var t = rig.subjects[i].target;
                if (t == null) continue;

                var renderer = t.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // extra slots count too - MoveOutline rides along on the sphere in front of it
                    var slots = renderer.sharedMaterials;
                    for (int slot = 0; slot < slots.Length; slot++)
                        if (slots[slot] != null && slots[slot].shader != null)
                            shadersInScene[slots[slot].shader] = new Placement(i, slot);
                }

                // 2D panels draw through a Graphic, so they'd otherwise look absent
                // and ticking Apply again would add a second copy
                foreach (var graphic in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    var m = graphic.material;
                    if (m == null || m == graphic.defaultMaterial || m.shader == null) continue;
                    shadersInScene[m.shader] = new Placement(i, 0);
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source != null) prefabsInScene[source] = i;
            }
        }

        var featureShaders = ShadersOnRendererFeatures();

        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) continue;
            if (shader.name.StartsWith("Hidden/")) continue; // plumbing, nothing to look at

            Placement at;
            bool on = shadersInScene.TryGetValue(shader, out at);
            _shaders.Add(new Row
            {
                asset = shader,
                name = GalleryScene.ShortName(shader),
                note = JobNote(shader, featureShaders),
                inScene = on,
                wanted = on,
                subjectIndex = on ? at.Subject : -1,
                slot = on ? at.Slot : 0,
                asPanel = on ? PanelInScene(rig, at.Subject) : GalleryScene.IsUIShader(shader)
            });
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<ParticleSystem>(true) == null) continue;

            int index;
            bool on = prefabsInScene.TryGetValue(prefab, out index);
            _particles.Add(new Row
            {
                asset = prefab,
                name = prefab.name,
                note = "",
                inScene = on,
                wanted = on,
                subjectIndex = on ? index : -1
            });
        }

        _shaders.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        _particles.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        Repaint();
    }

    /// <summary>
    /// Say where a shader already has a job, so it's clear a sphere won't tell you much
    /// about it. Read off the scene and the renderer rather than guessed from the passes.
    /// </summary>
    static string JobNote(Shader shader, HashSet<Shader> featureShaders)
    {
        var sky = RenderSettings.skybox;
        if (sky != null && sky.shader == shader) return "the skybox";
        if (featureShaders.Contains(shader)) return "a renderer feature";
        return "";
    }

    /// <summary>Shaders driving full-screen passes on the active URP renderer.</summary>
    static HashSet<Shader> ShadersOnRendererFeatures()
    {
        var found = new HashSet<Shader>();

        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
            as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp == null) return found;

        var field = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField(
            "m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = field != null
            ? field.GetValue(urp) as UnityEngine.Rendering.Universal.ScriptableRendererData[]
            : null;
        if (list == null) return found;

        foreach (var data in list)
        {
            if (data == null) continue;
            foreach (var feature in data.rendererFeatures)
            {
                if (feature == null) continue;

                var serialized = new SerializedObject(feature);
                var it = serialized.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                    var mat = it.objectReferenceValue as Material;
                    if (mat != null && mat.shader != null) found.Add(mat.shader);
                }
            }
        }

        return found;
    }

    static bool PanelInScene(GalleryRig rig, int index)
    {
        return index >= 0 && index < rig.subjects.Length && rig.subjects[index].flat;
    }

    bool HasChanges()
    {
        int a = 0, r = 0;
        CountChanges(ref a, ref r);
        return a > 0 || r > 0;
    }

    void CountChanges(ref int add, ref int remove)
    {
        foreach (var row in _shaders) Count(row, ref add, ref remove);
        foreach (var row in _particles) Count(row, ref add, ref remove);
    }

    static void Count(Row row, ref int add, ref int remove)
    {
        if (row.wanted && !row.inScene) add++;
        else if (!row.wanted && row.inScene) remove++;
    }

    void Apply(GalleryRig rig)
    {
        // a shader in an extra slot shares its sphere with another one, so drop the
        // material rather than the whole subject
        foreach (var row in _shaders)
            if (!row.wanted && row.inScene && row.slot > 0)
                GalleryScene.RemoveMaterialSlot(rig, row.subjectIndex, row.slot);

        // then the whole subjects, back to front - every removal shifts the indices after it
        var toRemove = new List<int>();
        foreach (var row in _shaders) if (!row.wanted && row.inScene && row.slot == 0) toRemove.Add(row.subjectIndex);
        foreach (var row in _particles) if (!row.wanted && row.inScene) toRemove.Add(row.subjectIndex);
        toRemove.Sort();
        for (int i = toRemove.Count - 1; i >= 0; i--) GalleryScene.Remove(rig, toRemove[i]);

        foreach (var row in _shaders)
        {
            if (!row.wanted || row.inScene) continue;

            if (row.asPanel) GalleryScene.AddCanvas(rig, (Shader)row.asset);
            else GalleryScene.AddShader(rig, (Shader)row.asset);
        }

        foreach (var row in _particles)
            if (row.wanted && !row.inScene) GalleryScene.AddParticles(rig, (GameObject)row.asset);

        GalleryScene.Layout(rig);
        AssetDatabase.SaveAssets();
        Scan();
    }
}
