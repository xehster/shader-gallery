using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The gallery's control panel. What gets used every session sits at the top;
/// everything else folds away so the inspector doesn't turn into a wall.
/// </summary>
[CustomEditor(typeof(GalleryRig))]
public class GalleryRigEditor : Editor
{
    // Only the ones worth flipping from here. ChromaFringes is deliberately absent: it
    // exists solely to draw the two fringe passes of PS1 Lit Chromatic, and that shader
    // already has Chromatic Shift, which reaches zero. Leaving both would be two
    // switches for one effect, and the material one is where you'd look for it.
    static readonly string[] TrackedFeatures =
    {
        "RetroDither", "HeightFog", "ScreenSpaceAmbientOcclusion", "CrtVhs"
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
        DrawCloseUps(rig);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Shader list…")) ShaderList.Open();
            DrawLabelToggle();
        }

        EditorGUILayout.Space(6f);
        if (Section("recording", "Recording")) DrawRecording(rig);
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

    void DrawCloseUps(GalleryRig rig)
    {
        if (rig.galleryCamera == null)
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

    void DrawRecording(GalleryRig rig)
    {
        int fps = EditorPrefs.GetInt(RecordFps, 12);
        int width = EditorPrefs.GetInt(RecordWidth, 900);

        EditorGUI.BeginChangeCheck();
        fps = EditorGUILayout.IntSlider("Frames per second", fps, 8, 30);
        width = EditorGUILayout.IntPopup("Width", width,
            new[] { "480", "640", "900", "1200" }, new[] { 480, 640, 900, 1200 });
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetInt(RecordFps, fps);
            EditorPrefs.SetInt(RecordWidth, width);
        }

        float loop = GalleryRecorder.LoopLength(rig);
        EditorGUILayout.LabelField(
            "One loop is " + loop.ToString("0.0") + "s, so " + Mathf.RoundToInt(loop * fps) + " frames",
            EditorStyles.miniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Record the gallery")) Record(rig, fps, width, "docs/gallery.gif");

            bool focused = rig.FocusIndex >= 0;
            using (new EditorGUI.DisabledScope(!focused))
            {
                string name = focused ? rig.subjects[rig.FocusIndex].label : "nothing";
                if (GUILayout.Button("Record " + name))
                    Record(rig, fps, width, "docs/" + Slug(name) + ".gif");
            }
        }

        if (rig.FocusIndex < 0)
            EditorGUILayout.LabelField("Pick a close-up above to record a single shader.", EditorStyles.miniLabel);
    }

    static void Record(GalleryRig rig, int fps, int width, string path)
    {
        if (GalleryRecorder.Record(rig, fps, width, path)) return;
        if (GalleryRecorder.LastError == "Cancelled.") return;

        EditorUtility.DisplayDialog("Recording failed", GalleryRecorder.LastError, "OK");
    }

    static string Slug(string name)
    {
        var s = new System.Text.StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) s.Append(c);
            else if (s.Length > 0 && s[s.Length - 1] != '-') s.Append('-');
        }
        return s.ToString().Trim('-');
    }

    const string RecordFps = "ShaderGallery.record.fps";
    const string RecordWidth = "ShaderGallery.record.width";

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
        Field("galleryCamera", "Camera");
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
    /// <summary>
    /// One entry per exhibit, holding everything that exhibit is made of. Some are just a
    /// material, some are a material plus the scripts that drive it, and grouping by
    /// subject keeps those halves together instead of scattering them down a flat list.
    ///
    /// A shared material shows under the first subject using it. Listing it again lower
    /// down would suggest the two copies are separate, and they are not.
    /// </summary>
    void DrawMaterialSettings(GalleryRig rig)
    {
        if (rig.subjects == null) return;

        var seenMaterials = new HashSet<Material>();
        var materials = new List<Material>();
        var scripts = new List<MonoBehaviour>();

        foreach (var subject in rig.subjects)
        {
            if (subject == null || subject.target == null) continue;

            materials.Clear();
            bool takesHits = false;
            foreach (var mat in rig.MaterialsOf(subject))
            {
                // asked of every material of the subject, not just the ones listed below,
                // so a shared material already shown elsewhere still brings the toggle here
                if (mat.HasProperty("_AcceptsImpacts")) takesHits = true;
                if (seenMaterials.Add(mat)) materials.Add(mat);
            }

            scripts.Clear();
            foreach (var behaviour in rig.TunablesOf(subject)) scripts.Add(behaviour);

            int parts = materials.Count + scripts.Count + (takesHits ? 1 : 0);
            if (parts == 0) continue;
            if (!Section("subject." + subject.label, subject.label)) continue;

            using (new EditorGUI.IndentLevelScope())
            {
                // one part needs no second foldout: that would be a click for nothing
                bool split = parts > 1;

                foreach (var mat in materials)
                    DrawPart(split, "mat." + mat.name,
                        mat.shader != null ? GalleryScene.ShortName(mat.shader) : mat.name,
                        () =>
                        {
                            var editor = MaterialEditorFor(mat);
                            if (editor != null) editor.PropertiesGUI();
                        });

                foreach (var behaviour in scripts)
                    DrawPart(split, "tune." + behaviour.GetType().Name + "." + behaviour.name,
                        ObjectNames.NicifyVariableName(behaviour.GetType().Name),
                        () =>
                        {
                            var editor = EditorFor(behaviour);
                            if (editor != null) editor.OnInspectorGUI();
                        });

                // The rig lands the hits, not the material, so these two live on the rig.
                // They still belong under the shader that shows them, which is where
                // anyone would go looking for them.
                if (takesHits)
                    DrawPart(split, "hits." + subject.label, "Impacts", () => DrawImpacts(rig));
            }
        }
    }

    void DrawImpacts(GalleryRig rig)
    {
        Field("impacts", "Keep hitting it");

        using (new EditorGUI.DisabledScope(!rig.impacts))
            Field("impactInterval", "Seconds between hits");

        if (!rig.impacts) return;

        if (!Application.isPlaying && !rig.animateInEditMode)
            EditorGUILayout.HelpBox(
                "Run in Edit Mode is off under Motion settings, so the hits only land in Play.",
                MessageType.Info);
    }

    static void DrawPart(bool foldout, string key, string title, System.Action body)
    {
        if (!foldout)
        {
            body();
            return;
        }

        if (!Section(key, title)) return;
        using (new EditorGUI.IndentLevelScope()) body();
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
    readonly Dictionary<Object, Editor> _componentEditors = new Dictionary<Object, Editor>();

    void OnDisable()
    {
        foreach (var editor in _materialEditors.Values)
            if (editor != null) DestroyImmediate(editor);
        _materialEditors.Clear();

        foreach (var editor in _componentEditors.Values)
            if (editor != null) DestroyImmediate(editor);
        _componentEditors.Clear();
    }

    Editor EditorFor(Object target)
    {
        Editor editor;
        if (_componentEditors.TryGetValue(target, out editor) && editor != null) return editor;

        editor = CreateEditor(target);
        _componentEditors[target] = editor;
        return editor;
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
        EditorGUILayout.LabelField("Greyed out ones belong to one shader; tune those in Shader settings.",
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

                // Game view is not a SceneView, so RepaintAll() there leaves it showing
                // the frame from before the toggle and the change looks like it missed.
                InternalEditorUtility.RepaintAllViews();
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
        // without SetDirty() the renderer isn't rebuilt and the toggle changes nothing.
        // It is public on ScriptableRendererData, so the lookup has to allow for that:
        // asking for NonPublic alone finds nothing and the toggle goes quiet.
        var m = typeof(ScriptableRendererData).GetMethod("SetDirty",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null) m.Invoke(data, null);
    }
}
