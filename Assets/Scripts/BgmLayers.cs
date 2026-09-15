using System.Collections;
using UnityEngine;

public class BgmLayers : MonoBehaviour
{
    [SerializeField] AudioSource build; // intro + between-waves
    [SerializeField] AudioSource wave;

    [SerializeField] AudioClip introClip;
    [SerializeField] AudioClip buildClip;
    [SerializeField] AudioClip waveClip;

    [SerializeField] float fadeTime = 1.2f;
    [SerializeField] float introVol = 0.3f;
    [SerializeField] float buildVol = 0.4f;
    [SerializeField] float waveVol = 0.45f;

    Coroutine fadeCo;

    float Scaled(float authored) => authored * GameSettings.MusicScale;

    void Awake()
    {
        GameSettings.EnsureExists();
        Setup(build);
        Setup(wave);
        GameSettings.Instance?.RegisterMusic(build);
        GameSettings.Instance?.RegisterMusic(wave);

        if (wave != null && waveClip != null)
        {
            wave.clip = waveClip;
            wave.volume = 0f;
            wave.Play();
        }
        StartIntro();
    }

    static void Setup(AudioSource s)
    {
        if (s == null) return;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.priority = 0;
        s.volume = 0f;
    }

    public void StartIntro()
    {
        StopFade();
        if (build == null || introClip == null) return;
        build.clip = introClip;
        build.volume = Scaled(introVol);
        if (!build.isPlaying) build.Play();
        if (wave != null) wave.volume = 0f;
    }

    public void ToBuild()
    {
        if (build == null) return;
        if (build.clip == introClip && buildClip != null)
            StartCoroutine(SwapBuildClipThenFocus());
        else
            Focus(build, Scaled(buildVol));
    }

    public void ToWave()
    {
        if (wave == null) return;
        if (wave.clip != waveClip && waveClip != null) wave.clip = waveClip;
        if (!wave.isPlaying) wave.Play();
        Focus(wave, Scaled(waveVol));
    }

    /// <summary>Re-apply volumes after GameSettings music slider changes.</summary>
    public void RefreshVolumesFromSettings()
    {
        if (fadeCo != null) return;
        if (wave != null && wave.volume > 0.01f)
            wave.volume = Scaled(waveVol);
        else if (build != null && build.isPlaying)
        {
            bool intro = build.clip == introClip;
            build.volume = Scaled(intro ? introVol : buildVol);
        }
    }

    void Focus(AudioSource target, float vol)
    {
        StopFade();
        fadeCo = StartCoroutine(FadeFocus(target, vol));
    }

    void StopFade()
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = null;
    }

    IEnumerator SwapBuildClipThenFocus()
    {
        float start = build.volume;
        float t = 0f;
        float half = fadeTime * 0.35f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            build.volume = Mathf.Lerp(start, 0f, t / half);
            if (wave) wave.volume = 0f;
            yield return null;
        }
        build.clip = buildClip;
        if (!build.isPlaying) build.Play();
        Focus(build, Scaled(buildVol));
    }

    IEnumerator FadeFocus(AudioSource target, float targetVol)
    {
        float buildStart = build != null ? build.volume : 0f;
        float waveStart = wave != null ? wave.volume : 0f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / fadeTime);
            if (build) build.volume = Mathf.Lerp(buildStart, target == build ? targetVol : 0f, u);
            if (wave) wave.volume = Mathf.Lerp(waveStart, target == wave ? targetVol : 0f, u);
            yield return null;
        }
        if (build) build.volume = target == build ? targetVol : 0f;
        if (wave) wave.volume = target == wave ? targetVol : 0f;
        fadeCo = null;
    }
}
