using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drives the row: how the subjects move, where the camera looks, what's on show.
/// Runs in Edit Mode as well as Play Mode, so footage can be recorded straight
/// from the Game View without entering play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class GalleryRig : MonoBehaviour
{
    [System.Serializable]
    public class Subject
    {
        public string label;
        public Transform target;
        public GameObject labelObject;
        [Tooltip("Clear this for anything a bounce would ruin - particles, ribbons, UI.")]
        public bool animate = true;
        [Tooltip("A flat panel rather than a sphere: it hops, but never spins or squashes.")]
        public bool flat;
        public Vector3 restPosition;
        public Vector3 restScale = Vector3.one;
        public Quaternion restRotation = Quaternion.identity;
    }

    public Subject[] subjects = new Subject[0];

    [Header("2D panels")]
    [Tooltip("The picture 2D shaders recolour. Usually the live render of the reference sphere.")]
    public Texture sampleTexture;
    [Tooltip("Camera pointed at the reference sphere. Rendered by hand so the picture updates in Edit Mode.")]
    public Camera sampleCamera;
    [Tooltip("What the sample camera renders into, before the circle is cut out of it.")]
    public RenderTexture sampleSource;
    [Tooltip("Blit material that punches the circular alpha. Hidden/Gallery/CircleCut.")]
    public Material sampleCutter;
    [Tooltip("The reference sphere itself. Turns slowly so the panels aren't showing a still.")]
    public Transform sampleSubject;

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
    [FormerlySerializedAs("labCamera")] public Camera galleryCamera;
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
        RenderSample(now);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Particle systems sit frozen in Edit Mode, so we step them by hand.
    /// Otherwise the fire only shows up in play and can't be recorded from the editor.
    /// </summary>
    void StepParticles(float now)
    {
        float dt = Mathf.Clamp(now - _lastParticleTime, 0f, 0.1f);
        _lastParticleTime = now;
        StepParticlesBy(dt);
    }

    /// <summary>Advance every particle system by an exact amount.</summary>
    public void StepParticlesBy(float dt)
    {
        if (subjects == null || dt <= 0f) return;

        foreach (var ps in ParticleSystems())
        {
            if (!ps.isPlaying) ps.Play(true);
            ps.Simulate(dt, true, false, false);
        }
    }

    /// <summary>Empty them out, so a recording always starts from the same frame.</summary>
    public void ResetParticles()
    {
        foreach (var ps in ParticleSystems())
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    IEnumerable<ParticleSystem> ParticleSystems()
    {
        if (subjects == null) yield break;

        foreach (var s in subjects)
        {
            if (s == null || s.target == null) continue;

            var ps = s.target.GetComponent<ParticleSystem>();
            if (ps != null) yield return ps;
        }
    }

    /// <summary>
    /// Put the row exactly where it belongs at this moment, without waiting for the
    /// editor to tick. Animate() is a plain function of time, so frames come out evenly
    /// spaced however much the editor stutters while recording.
    /// </summary>
    public void SampleAt(float time, float particleStep)
    {
        Animate(time);
        StepParticlesBy(particleStep);
    }

    float _lastParticleTime;

    /// <summary>
    /// Unity doesn't drive an off-screen camera in Edit Mode, so the picture the 2D
    /// panels recolour would be a frozen frame. Turn the reference sphere and render it.
    /// </summary>
    void RenderSample(float now)
    {
        if (sampleCamera == null) return;

        if (sampleSubject != null)
            sampleSubject.rotation = Quaternion.Euler(-15f, now * spinDegreesPerSecond, 0f);

        sampleCamera.Render();

        // the camera fills a square; cut it to a circle so the panels show the sphere alone
        var target = sampleTexture as RenderTexture;
        if (sampleCutter != null && sampleSource != null && target != null)
            Graphics.Blit(sampleSource, target, sampleCutter);
    }
#endif

    public int FocusIndex { get { return focusIndex; } }

    /// <summary>Remember where the camera is now as the wide shot.</summary>
    public void SaveWideShot()
    {
        if (galleryCamera == null) return;
        wideCamPosition = galleryCamera.transform.position;
        wideCamEuler = galleryCamera.transform.rotation.eulerAngles;
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
        if (galleryCamera == null) return;

        if (focusIndex < 0 || subjects == null || focusIndex >= subjects.Length)
        {
            if (wideShotSaved)
            {
                galleryCamera.transform.position = wideCamPosition;
                galleryCamera.transform.rotation = Quaternion.Euler(wideCamEuler);
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

        galleryCamera.transform.position = look + new Vector3(0f, closeUpHeight, -distance);
        galleryCamera.transform.LookAt(look);
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
                // a panel spun on its axis would just turn edge-on to the camera
                if (!s.flat) s.target.rotation = Quaternion.Euler(-15f, time * spinDegreesPerSecond, 0f);
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

            float sy = 1f;
            if (!s.flat)
            {
                sy = Mathf.Max(0.2f, 1f + stretch * speedNorm * (1f - contact) - squash * contact);
                float sxz = 1f / Mathf.Sqrt(sy); // keep the volume, which is what sells the rubbery look
                s.target.localScale = new Vector3(s.restScale.x * sxz, s.restScale.y * sy, s.restScale.z * sxz);
            }

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
            if (renderer != null)
                foreach (var m in renderer.sharedMaterials)
                    if (m != null && seen.Add(m)) yield return m;

            // 2D panels draw through a CanvasRenderer, so their material isn't on a Renderer.
            // Skip graphics left on the stock UI material - that's the mask, not a shader on show.
            foreach (var graphic in s.target.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                var m = graphic.material;
                if (m == null || m == graphic.defaultMaterial) continue;
                if (seen.Add(m)) yield return m;
            }
        }
    }

}
