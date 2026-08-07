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
        public string note;      // why a shader might not suit a sphere
        public bool inScene;     // how things are
        public bool wanted;      // how the user wants them
        public int subjectIndex; // -1 when not in the scene
    }

    static readonly string[] MeshLightModes =
    {
        "UniversalForward", "UniversalForwardOnly", "UniversalGBuffer", "SRPDefaultUnlit"
    };

    readonly List<Row> _shaders = new List<Row>();
    readonly List<Row> _particles = new List<Row>();
    Vector2 _scroll;
    bool _scanned;

    [MenuItem("ShaderLab/Shader List")]
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
        var rig = ShaderLabScene.FindRig();
        if (rig == null)
        {
            EditorGUILayout.HelpBox("Open the ShaderLab scene - this window works on the rig in it.", MessageType.Info);
            if (GUILayout.Button("Open scene"))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ShaderLab.unity");
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

                if (!string.IsNullOrEmpty(row.note))
                    EditorGUILayout.LabelField(row.note, EditorStyles.miniLabel, GUILayout.Width(150f));

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

        var rig = ShaderLabScene.FindRig();
        var shadersInScene = new Dictionary<Shader, int>();
        var prefabsInScene = new Dictionary<GameObject, int>();

        if (rig != null)
        {
            for (int i = 0; i < rig.subjects.Length; i++)
            {
                var t = rig.subjects[i].target;
                if (t == null) continue;

                var renderer = t.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                    shadersInScene[renderer.sharedMaterial.shader] = i;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if (source != null) prefabsInScene[source] = i;
            }
        }

        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/TextMesh Pro")) continue;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) continue;

            int index;
            bool on = shadersInScene.TryGetValue(shader, out index);
            _shaders.Add(new Row
            {
                asset = shader,
                name = ShaderLabScene.ShortName(shader),
                note = MeshNote(shader),
                inScene = on,
                wanted = on,
                subjectIndex = on ? index : -1
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
    /// Full-screen passes and the skybox have no geometry pass at all, so a sphere
    /// shows nothing. We flag those but leave the tick alone - MoveOutline lands in
    /// the same bucket and still works as a second material slot.
    /// </summary>
    static string MeshNote(Shader shader)
    {
        var tag = new UnityEngine.Rendering.ShaderTagId("LightMode");
        for (int i = 0; i < shader.passCount; i++)
        {
            string mode = shader.FindPassTagValue(i, tag).name;
            if (string.IsNullOrEmpty(mode)) continue;
            if (System.Array.IndexOf(MeshLightModes, mode) >= 0) return "";
        }
        return "won't draw on its own";
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

    void Apply(ShaderLabRig rig)
    {
        // remove first, back to front - every removal shifts the indices after it
        var toRemove = new List<int>();
        foreach (var row in _shaders) if (!row.wanted && row.inScene) toRemove.Add(row.subjectIndex);
        foreach (var row in _particles) if (!row.wanted && row.inScene) toRemove.Add(row.subjectIndex);
        toRemove.Sort();
        for (int i = toRemove.Count - 1; i >= 0; i--) ShaderLabScene.Remove(rig, toRemove[i]);

        foreach (var row in _shaders)
            if (row.wanted && !row.inScene) ShaderLabScene.AddShader(rig, (Shader)row.asset);

        foreach (var row in _particles)
            if (row.wanted && !row.inScene) ShaderLabScene.AddParticles(rig, (GameObject)row.asset);

        ShaderLabScene.Layout(rig);
        AssetDatabase.SaveAssets();
        Scan();
    }
}
