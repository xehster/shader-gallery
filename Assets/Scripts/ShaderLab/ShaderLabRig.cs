using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drives the showcase row and keeps every lab material setting in one place.
/// Runs in Edit Mode as well as Play Mode, so footage can be recorded straight
/// from the Game View without entering play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ShaderLabRig : MonoBehaviour
{
    [System.Serializable]
    public class Subject
    {
        public string label;
        public Transform target;
        public GameObject labelObject;
        [Tooltip("Clear this for anything a bounce would ruin - particles, ribbons, UI.")]
        public bool animate = true;
        public Vector3 restPosition;
        public Vector3 restScale = Vector3.one;
    }

    public Subject[] subjects = new Subject[0];

    [Header("Shelves")]
    [Tooltip("Subjects per shelf. Leave at 0 to work it out from how many there are; 10 is the ceiling either way.")]
    public int shelfColumns = 0;
    [Tooltip("Vertical gap between shelves. Jump height gets capped so nothing headbutts the shelf above.")]
    public float shelfSpacing = 5f;

    public enum Motion
    {
        Spin,    // turns in place
        Bounce,  // hops and squashes
        Static   // sits still
    }

    [Header("Motion")]
    public Motion motion = Motion.Bounce;
    [Tooltip("Keep animating in Edit Mode. Turn off if the editor feels sluggish.")]
    public bool animateInEditMode = true;
    [Tooltip("Jump height in metres. The spheres are 2 m across, so 6 is about three of them.")]
    public float jumpHeight = 6f;
    [Tooltip("Full cycle: one big hop plus the small springy one after it.")]
    public float cycleSeconds = 1.6f;
    [Tooltip("Phase shift between neighbours - this is what makes the wave. 0 moves them in sync.")]
    public float phaseOffset = 0.12f;
    [Range(0f, 0.6f), Tooltip("Height of the second hop, as a fraction of the first.")]
    public float secondaryHop = 0.28f;
    [Range(0f, 0.6f), Tooltip("How far it flattens on landing.")]
    public float squash = 0.32f;
    [Range(0f, 0.6f), Tooltip("How far it stretches at speed.")]
    public float stretch = 0.22f;
    [Tooltip("Spin around Y in degrees per second. Good for reading affine warp and triplanar projection.")]
    public float spinDegreesPerSecond = 20f;

    [Header("Camera")]
    public Camera labCamera;
    [Tooltip("How far the camera sits back on a close-up.")]
    public float closeUpDistance = 4.2f;
    [Tooltip("How high above its aim point the camera sits.")]
    public float closeUpHeight = 0.7f;
    [Range(0f, 1f), Tooltip("0 leaves the camera still and lets the subject leave frame, 1 tracks the jump all the way.")]
    public float closeUpFollow = 0.65f;
    [Tooltip("Hide the rest of the row and every label during a close-up.")]
    public bool soloFocus = true;
    [Tooltip("How far above its base to aim at subjects that don't move - particles fire upwards.")]
    public float staticLookHeight = 1.6f;
    [SerializeField, HideInInspector] int focusIndex = -1;
    [SerializeField, HideInInspector] bool wideShotSaved;
    [SerializeField, HideInInspector] Vector3 wideCamPosition;
    [SerializeField, HideInInspector] Vector3 wideCamEuler;

    [Header("PS1 look, across every lab material")]
    [Tooltip("Snap vertices to a virtual pixel grid, which makes silhouettes wobble.")]
    public bool ps1VertexSnap = true;
    [Tooltip("Height of that pixel grid. 240 is the value the game ships with.")]
    public float ps1SnapPixels = 240f;
    [Tooltip("Skip perspective correction on UVs, so textures swim across triangles.")]
    public bool ps1AffineWarp = true;
    [Range(0f, 1f), Tooltip("0 is a modern GPU, 1 is full 1996.")]
    public float ps1AffineAmount = 1f;

    void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        RestSubjects();
    }

    void Update()
    {
        if (Application.isPlaying)
            Animate(Time.timeSinceLevelLoad);
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying) return;
        if (!animateInEditMode) return;

        float now = (float)EditorApplication.timeSinceStartup;
        Animate(now);
        StepParticles(now);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Particle systems sit frozen in Edit Mode, so we step them by hand.
    /// Otherwise the fire only shows up in play and can't be recorded from the editor.
    /// </summary>
    void StepParticles(float now)
    {
        if (subjects == null) return;

        float dt = Mathf.Clamp(now - _lastParticleTime, 0f, 0.1f);
        _lastParticleTime = now;
        if (dt <= 0f) return;

        foreach (var s in subjects)
        {
            if (s == null || s.target == null) continue;

            var ps = s.target.GetComponent<ParticleSystem>();
            if (ps == null) continue;
            if (!ps.isPlaying) ps.Play(true);
            ps.Simulate(dt, true, false, false);
        }
    }

    float _lastParticleTime;
#endif

    public int FocusIndex { get { return focusIndex; } }

    /// <summary>Remember where the camera is now as the wide shot.</summary>
    public void SaveWideShot()
    {
        if (labCamera == null) return;
        wideCamPosition = labCamera.transform.position;
        wideCamEuler = labCamera.transform.rotation.eulerAngles;
        wideShotSaved = true;
    }

    public void FocusOn(int index)
    {
        if (subjects == null || index < 0 || index >= subjects.Length) return;
        if (focusIndex < 0) SaveWideShot();
        focusIndex = index;
        ApplySolo();
        UpdateCamera();
    }

    public void FocusWide()
    {
        focusIndex = -1;
        ApplySolo();
        UpdateCamera();
    }

    void ApplySolo()
    {
        if (subjects == null) return;

        bool solo = soloFocus && focusIndex >= 0;
        for (int i = 0; i < subjects.Length; i++)
        {
            var s = subjects[i];
            if (s == null) continue;
            if (s.target != null) s.target.gameObject.SetActive(!solo || i == focusIndex);
            if (s.labelObject != null) s.labelObject.SetActive(!solo);
        }
    }

    void UpdateCamera()
    {
        if (labCamera == null) return;

        if (focusIndex < 0 || subjects == null || focusIndex >= subjects.Length)
        {
            if (wideShotSaved)
            {
                labCamera.transform.position = wideCamPosition;
                labCamera.transform.rotation = Quaternion.Euler(wideCamEuler);
            }
            return;
        }

        var s = subjects[focusIndex];
        if (s == null || s.target == null) return;

        Vector3 look;
        float distance = closeUpDistance;

        if (s.animate)
        {
            float y = Mathf.Lerp(s.restPosition.y, s.target.position.y, closeUpFollow);
            look = new Vector3(s.restPosition.x, y, s.restPosition.z);
        }
        else
        {
            // Particles sit on the floor and fire upwards, so aim above the base.
            // Their bounds are no help - they cover the whole simulation volume.
            look = s.restPosition + Vector3.up * staticLookHeight;
            distance = closeUpDistance * 1.6f;
        }

        labCamera.transform.position = look + new Vector3(0f, closeUpHeight, -distance);
        labCamera.transform.LookAt(look);
    }

    void Animate(float time)
    {
        if (subjects == null) return;

        float cycle = Mathf.Max(0.1f, cycleSeconds);
        float bigDur = cycle * 0.68f;
        float smallDur = cycle - bigDur;

        for (int i = 0; i < subjects.Length; i++)
        {
            var s = subjects[i];
            if (s == null || s.target == null) continue;
            if (!s.animate) continue;

            if (motion == Motion.Static)
            {
                s.target.position = s.restPosition;
                s.target.localScale = s.restScale;
                continue;
            }

            if (motion == Motion.Spin)
            {
                s.target.position = s.restPosition;
                s.target.localScale = s.restScale;
                s.target.rotation = Quaternion.Euler(-15f, time * spinDegreesPerSecond, 0f);
                continue;
            }

            float radius = Mathf.Max(0.01f, s.restScale.y * 0.5f);

            float t = Mathf.Repeat(time + i * phaseOffset, cycle);
            float peak, dur, u;
            if (t < bigDur) { peak = jumpHeight; dur = bigDur; u = t / bigDur; }
            else { peak = jumpHeight * secondaryHop; dur = smallDur; u = (t - bigDur) / Mathf.Max(0.01f, smallDur); }

            float height = peak * 4f * u * (1f - u);
            float velocity = peak * 4f * (1f - 2f * u) / Mathf.Max(0.01f, dur);

            // 1 right at the floor, 0 once it has lifted off - the window where squash applies
            float contact = 1f - Mathf.Clamp01(height / Mathf.Max(0.01f, jumpHeight * 0.12f));
            float speedNorm = Mathf.Clamp01(Mathf.Abs(velocity) / Mathf.Max(0.01f, 4f * jumpHeight / bigDur));

            float sy = Mathf.Max(0.2f, 1f + stretch * speedNorm * (1f - contact) - squash * contact);
            float sxz = 1f / Mathf.Sqrt(sy); // keep the volume, which is what sells the rubbery look

            s.target.localScale = new Vector3(s.restScale.x * sxz, s.restScale.y * sy, s.restScale.z * sxz);

            // keep the bottom on the floor while it's being flattened
            float sink = (1f - sy) * radius;
            s.target.position = s.restPosition + new Vector3(0f, height - sink, 0f);
        }

        UpdateCamera();
    }

    /// <summary>Put everything back where it started.</summary>
    public void RestSubjects()
    {
        if (subjects == null) return;

        foreach (var s in subjects)
        {
            if (s == null || s.target == null) continue;
            s.target.position = s.restPosition;
            s.target.localScale = s.restScale;
        }
    }

    void OnValidate()
    {
        if (motion == Motion.Static) RestSubjects();
        ApplyMaterials();
        ApplySolo();
        UpdateCamera();
    }

    /// <summary>Every unique material sitting on a subject in the row.</summary>
    public IEnumerable<Material> Materials()
    {
        if (subjects == null) yield break;

        var seen = new HashSet<Material>();
        foreach (var s in subjects)
        {
            if (s == null || s.target == null) continue;

            var renderer = s.target.GetComponent<Renderer>();
            if (renderer == null) continue;

            foreach (var m in renderer.sharedMaterials)
                if (m != null && seen.Add(m)) yield return m;
        }
    }

    /// <summary>
    /// Push the PS1 toggles onto every material in the row. Per-shader settings are
    /// edited on the materials themselves, so there is nothing else to sync here.
    /// </summary>
    public void ApplyMaterials()
    {
        foreach (var m in Materials())
        {
            if (m.HasProperty("_VertexSnapPixels")) m.SetFloat("_VertexSnapPixels", SnapValue);
            if (m.HasProperty("_AffineAmount")) m.SetFloat("_AffineAmount", ps1AffineWarp ? ps1AffineAmount : 0f);
        }
    }

    /// <summary>Grid size with the toggle folded in: 0 means the shader skips snapping.</summary>
    float SnapValue { get { return ps1VertexSnap ? ps1SnapPixels : 0f; } }

    /// <summary>Read the PS1 values back off the first material that carries them.</summary>
    public void PullFromMaterials()
    {
        foreach (var m in Materials())
        {
            if (!m.HasProperty("_VertexSnapPixels")) continue;

            float snap = m.GetFloat("_VertexSnapPixels");
            ps1VertexSnap = snap >= 1f;
            if (ps1VertexSnap) ps1SnapPixels = snap;

            if (m.HasProperty("_AffineAmount"))
            {
                float affine = m.GetFloat("_AffineAmount");
                ps1AffineWarp = affine > 0f;
                if (ps1AffineWarp) ps1AffineAmount = affine;
            }
            return;
        }
    }
}
