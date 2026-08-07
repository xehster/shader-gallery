using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The lab's control panel. What gets used every session sits at the top;
/// everything else folds away so the inspector doesn't turn into a wall.
/// </summary>
[CustomEditor(typeof(GalleryRig))]
public class GalleryRigEditor : Editor
{
    static readonly string[] TrackedFeatures =
    {
        "RetroDither", "HeightFog", "ChromaFringes", "ScreenSpaceAmbientOcclusion"
    };

    static readonly GUIContent[] MotionTabs =
    {
        new GUIContent("Spin"),
        new GUIContent("Bounce"),
        new GUIContent("Still")
    };

    static readonly Color Highlight = new Color(0.55f, 0.85f, 1f);

    public override void OnInspectorGUI()
    {
        var rig = (GalleryRig)target;

        DrawMotion(rig);
        EditorGUILayout.Space(2f);
        DrawPS1(rig);
        EditorGUILayout.Space(2f);
        DrawCloseUps(rig);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Shader list…")) ShaderList.Open();
            DrawLabelToggle();
        }

        EditorGUILayout.Space(6f);
        if (Section("shelves", "Shelves")) DrawShelfSettings(rig);
        if (Section("motion", "Motion settings")) DrawMotionDetails(rig);
        if (Section("camera", "Camera")) DrawCameraSettings(rig);
        if (Section("materials", "Shader settings")) DrawMaterialSettings(rig);
        if (Section("features", "Renderer features")) DrawRendererFeatures();
        if (Section("subjects", "Subjects")) DrawSubjects();
    }

    // --- always visible ------------------------------------------------------

    void DrawMotion(GalleryRig rig)
    {
        int now = (int)rig.motion;
        int picked = GUILayout.Toolbar(now, MotionTabs, GUILayout.Height(24f));
        if (picked == now) return;

        Undo.RecordObject(rig, "Gallery motion");
        rig.motion = (GalleryRig.Motion)picked;
        if (rig.motion == GalleryRig.Motion.Static) rig.RestSubjects();
        Touch(rig);
    }

    void DrawPS1(GalleryRig rig)
    {
        EditorGUI.BeginChangeCheck();
        bool snap = EditorGUILayout.ToggleLeft("Vertex snap", rig.ps1VertexSnap);
        bool affine = EditorGUILayout.ToggleLeft("Affine warp", rig.ps1AffineWarp);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(rig, "PS1 toggle");
        rig.ps1VertexSnap = snap;
        rig.ps1AffineWarp = affine;
        rig.ApplyMaterials();
        SaveMaterials(rig);
        Touch(rig);
    }

    void DrawCloseUps(GalleryRig rig)
    {
        if (rig.labCamera == null)
        {
            EditorGUILayout.HelpBox("No camera set, so close-ups do nothing.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Close-up", EditorStyles.boldLabel);

        for (int i = 0; i < rig.subjects.Length; i++)
        {
            if (i % 2 == 0) EditorGUILayout.BeginHorizontal();

            var bg = GUI.backgroundColor;
            if (rig.FocusIndex == i) GUI.backgroundColor = Highlight;

            string label = string.IsNullOrEmpty(rig.subjects[i].label) ? "Subject " + (i + 1) : rig.subjects[i].label;
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(21f)))
            {
                Undo.RecordObject(rig, "Close-up");
                rig.FocusOn(i);
                Touch(rig);
            }

            GUI.backgroundColor = bg;
            if (i % 2 == 1 || i == rig.subjects.Length - 1) EditorGUILayout.EndHorizontal();
        }

        var wideBg = GUI.backgroundColor;
        if (rig.FocusIndex < 0) GUI.backgroundColor = Highlight;
        if (GUILayout.Button("Wide shot", GUILayout.Height(22f)))
        {
            Undo.RecordObject(rig, "Wide shot");
            rig.FocusWide();
            Touch(rig);
        }
        GUI.backgroundColor = wideBg;
    }

    // --- folded sections -----------------------------------------------------

    void DrawShelfSettings(GalleryRig rig)
    {
        EditorGUI.BeginChangeCheck();

        int columns = EditorGUILayout.IntSlider(
            new GUIContent("Per shelf", "0 works it out from the subject count."), rig.shelfColumns, 0, 10);
        float spacing = EditorGUILayout.Slider("Spacing", rig.shelfSpacing, 3f, 12f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(rig, "Shelf layout");
            rig.shelfColumns = columns;
            rig.shelfSpacing = spacing;
            GalleryScene.Layout(rig);
        }

        EditorGUILayout.LabelField(Breakdown(rig), EditorStyles.miniLabel);

        if (!GUILayout.Button("Rebuild")) return;

        GalleryScene.Layout(rig);
        Touch(rig);
    }

    /// <summary>Spells out how the subjects will sit, e.g. "11 subjects: 6+5".</summary>
    static string Breakdown(GalleryRig rig)
    {
        int n = rig.subjects.Length;
        if (n == 0) return "nothing on the shelves yet";

        int per = GalleryScene.ColumnsFor(rig);
        var parts = new List<string>();
        for (int left = n; left > 0; left -= per) parts.Add(Mathf.Min(per, left).ToString());

        return n + " subjects: " + string.Join("+", parts.ToArray());
    }

    void DrawMotionDetails(GalleryRig rig)
    {
        Field("animateInEditMode", "Run in Edit Mode");

        if (rig.motion == GalleryRig.Motion.Spin)
        {
            Field("spinDegreesPerSecond", "Degrees per second");
        }
        else if (rig.motion == GalleryRig.Motion.Bounce)
        {
            Field("jumpHeight", "Jump height");
            Field("cycleSeconds", "Cycle, seconds");
            Field("phaseOffset", "Phase offset");
            Field("secondaryHop", "Second hop");
            Field("squash", "Squash");
            Field("stretch", "Stretch");
        }
        else
        {
            EditorGUILayout.LabelField("Nothing to tune while everything sits still.", EditorStyles.miniLabel);
        }
    }

    void DrawCameraSettings(GalleryRig rig)
    {
        Field("labCamera", "Camera");
        Field("closeUpDistance", "Distance");
        Field("closeUpHeight", "Height");
        Field("closeUpFollow", "Follow the jump");
        Field("soloFocus", "Hide the others");
        Field("staticLookHeight", "Aim height for still subjects");

        if (!GUILayout.Button("Save current view as the wide shot")) return;

        Undo.RecordObject(rig, "Save wide shot");
        rig.SaveWideShot();
        Touch(rig);
    }

    /// <summary>
    /// One foldout per material in the row, each holding that shader's own properties.
    /// Nothing is hard-coded, so a shader added through the Shader List brings its
    /// settings along with it.
    /// </summary>
    void DrawMaterialSettings(GalleryRig rig)
    {
        EditorGUI.BeginChangeCheck();
        Field("ps1SnapPixels", "PS1 grid, px");
        Field("ps1AffineAmount", "PS1 warp amount");
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            rig.ApplyMaterials();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4f);

        foreach (var mat in rig.Materials())
        {
            string title = mat.shader != null ? GalleryScene.ShortName(mat.shader) : mat.name;
            if (!Section("mat." + mat.name, title)) continue;

            using (new EditorGUI.IndentLevelScope())
            {
                var editor = MaterialEditorFor(mat);
                if (editor != null) editor.PropertiesGUI();
            }
        }
    }

    MaterialEditor MaterialEditorFor(Material mat)
    {
        MaterialEditor editor;
        if (_materialEditors.TryGetValue(mat, out editor) && editor != null) return editor;

        editor = CreateEditor(mat) as MaterialEditor;
        _materialEditors[mat] = editor;
        return editor;
    }

    readonly Dictionary<Material, MaterialEditor> _materialEditors = new Dictionary<Material, MaterialEditor>();

    void OnDisable()
    {
        foreach (var editor in _materialEditors.Values)
            if (editor != null) DestroyImmediate(editor);
        _materialEditors.Clear();
    }

    /// <summary>Labels off makes for a cleaner recording, so this one stays in reach.</summary>
    static void DrawLabelToggle()
    {
        var root = GameObject.Find("Spheres");
        if (root == null || !GUILayout.Button("Labels")) return;

        foreach (var tmp in root.GetComponentsInChildren<TMPro.TextMeshPro>(true))
        {
            Undo.RecordObject(tmp.gameObject, "Toggle labels");
            tmp.gameObject.SetActive(!tmp.gameObject.activeSelf);
        }
        SceneView.RepaintAll();
    }

    void DrawSubjects()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("subjects"), true);
        if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
    }

    void DrawRendererFeatures()
    {
        var data = RendererData();
        if (data == null)
        {
            EditorGUILayout.LabelField("No URP renderer found.", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField("Shared with the whole project - put them back after a shoot.",
            EditorStyles.miniLabel);

        foreach (var feature in data.rendererFeatures)
        {
            if (feature == null) continue;
            bool tracked = System.Array.IndexOf(TrackedFeatures, feature.name) >= 0;

            using (new EditorGUI.DisabledScope(!tracked))
            {
                bool now = EditorGUILayout.ToggleLeft(feature.name, feature.isActive);
                if (now == feature.isActive) continue;

                Undo.RecordObject(feature, "Toggle feature");
                feature.SetActive(now);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
                MarkRendererDirty(data);
                SceneView.RepaintAll();
            }
        }
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>Foldout whose state survives a restart.</summary>
    static bool Section(string key, string title)
    {
        string pref = "ShaderGallery.section." + key;
        bool open = EditorPrefs.GetBool(pref, false);
        bool now = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
        if (now != open) EditorPrefs.SetBool(pref, now);
        return now;
    }

    void Field(string property, string label)
    {
        var prop = serializedObject.FindProperty(property);
        if (prop == null) return;

        EditorGUILayout.PropertyField(prop, new GUIContent(label));
        serializedObject.ApplyModifiedProperties();
    }

    static void Touch(GalleryRig rig)
    {
        EditorUtility.SetDirty(rig);
        GalleryScene.MarkSceneDirty(rig);
        SceneView.RepaintAll();
    }

    static void SaveMaterials(GalleryRig rig)
    {
        foreach (var m in rig.Materials())
            EditorUtility.SetDirty(m);

        AssetDatabase.SaveAssets();
    }

    static ScriptableRendererData RendererData()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return null;

        var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var list = field != null ? field.GetValue(urp) as ScriptableRendererData[] : null;
        return list != null && list.Length > 0 ? list[0] : null;
    }

    static void MarkRendererDirty(ScriptableRendererData data)
    {
        // without SetDirty() the renderer isn't rebuilt and the toggle changes nothing
        var m = typeof(ScriptableRendererData).GetMethod("SetDirty",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null) m.Invoke(data, null);
    }
}
