using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Renders the gallery frame by frame and hands the frames to ffmpeg.
/// Not a screen capture: every frame asks the rig where things are at an exact moment,
/// so the spacing is even no matter how the editor was feeling, and the loop closes.
/// </summary>
public static class GalleryRecorder
{
    // two seconds of particles before the first frame, or the fire starts mid-puff
    const float WarmUp = 2f;
    const int Supersample = 2;

    public static string LastError { get; private set; }

    public static bool Record(GalleryRig rig, int fps, int width, string outputPath)
    {
        LastError = null;

        var cam = rig.galleryCamera;
        if (cam == null) { LastError = "No camera on the rig."; return false; }

        string ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
        {
            LastError = "ffmpeg not found. Put it on PATH or install it with: winget install Gyan.FFmpeg";
            return false;
        }

        float duration = LoopLength(rig);
        int frames = Mathf.Max(2, Mathf.RoundToInt(duration * fps));
        float step = duration / frames;

        string dir = Path.Combine(Path.GetTempPath(), "gallery-frames");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        bool wasAnimating = rig.animateInEditMode;
        var previousTarget = cam.targetTexture;
        RenderTexture rt = null;

        try
        {
            // the editor tick would fight us for the clock
            rig.animateInEditMode = false;
            rig.ResetParticles();
            for (float t = 0f; t < WarmUp; t += step) rig.StepParticlesBy(step);

            int height = Mathf.RoundToInt(width / cam.aspect / 2f) * 2;
            rt = new RenderTexture(width * Supersample, height * Supersample, 24);
            var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            for (int i = 0; i < frames; i++)
            {
                float t = i * step;
                rig.SampleAt(t, step);
                RenderReferenceSphere(rig, i / (float)frames);

                cam.Render();
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(dir, "f_" + i.ToString("D4") + ".png"), shot.EncodeToPNG());

                if (EditorUtility.DisplayCancelableProgressBar("Recording", "Frame " + (i + 1) + " of " + frames,
                        (i + 1) / (float)frames))
                {
                    LastError = "Cancelled.";
                    return false;
                }
            }

            Object.DestroyImmediate(shot);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            cam.targetTexture = previousTarget;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            rig.animateInEditMode = wasAnimating;
        }

        return Encode(ffmpeg, dir, fps, width, outputPath);
    }

    /// <summary>
    /// How long one full turn of the animation takes. Recording exactly this much makes
    /// the gif loop without a jump.
    /// </summary>
    public static float LoopLength(GalleryRig rig)
    {
        if (rig.motion == GalleryRig.Motion.Bounce) return Mathf.Max(0.2f, rig.cycleSeconds);

        if (rig.motion == GalleryRig.Motion.Spin && Mathf.Abs(rig.spinDegreesPerSecond) > 0.01f)
            return Mathf.Abs(360f / rig.spinDegreesPerSecond);

        return 2f;
    }

    /// <summary>
    /// The reference sphere feeding the 2D panels turns on its own clock, which would
    /// leave a seam. Give it exactly one turn across the recording instead.
    /// </summary>
    static void RenderReferenceSphere(GalleryRig rig, float progress)
    {
        if (rig.sampleCamera == null) return;

        if (rig.sampleSubject != null)
            rig.sampleSubject.rotation = Quaternion.Euler(-15f, progress * 360f, 0f);

        rig.sampleCamera.Render();

        var target = rig.sampleTexture as RenderTexture;
        if (rig.sampleCutter != null && rig.sampleSource != null && target != null)
            Graphics.Blit(rig.sampleSource, target, rig.sampleCutter);
    }

    static bool Encode(string ffmpeg, string frameDir, int fps, int width, string outputPath)
    {
        string full = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));

        // no dithering in the palette: RetroDither already puts noise on every pixel and
        // a second layer doubles the file for nothing
        string filter = "scale=" + width + ":-2:flags=lanczos,split[a][b];" +
                        "[a]palettegen=max_colors=128:stats_mode=diff[p];" +
                        "[b][p]paletteuse=dither=none:diff_mode=rectangle";

        var p = new Process();
        p.StartInfo.FileName = ffmpeg;
        p.StartInfo.Arguments = "-y -v error -framerate " + fps +
                                " -i \"" + Path.Combine(frameDir, "f_%04d.png") + "\"" +
                                " -vf \"" + filter + "\" -loop 0 \"" + full + "\"";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;

        p.Start();
        string errors = p.StandardError.ReadToEnd();
        p.WaitForExit();

        Directory.Delete(frameDir, true);

        if (p.ExitCode != 0)
        {
            LastError = "ffmpeg failed: " + errors.Trim();
            return false;
        }

        AssetDatabase.Refresh();
        Debug.Log("Recorded " + outputPath + " (" + new FileInfo(full).Length / 1024 + " KB)");
        return true;
    }

    static string FindFfmpeg()
    {
        if (Runs("ffmpeg")) return "ffmpeg";

        // winget drops it here and only adds it to PATH after a shell restart
        string winget = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");

        if (!Directory.Exists(winget)) return null;

        foreach (var file in Directory.GetFiles(winget, "ffmpeg.exe", SearchOption.AllDirectories))
            return file;

        return null;
    }

    static bool Runs(string exe)
    {
        try
        {
            var p = new Process();
            p.StartInfo.FileName = exe;
            p.StartInfo.Arguments = "-version";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
