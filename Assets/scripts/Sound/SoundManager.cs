using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private const string EffectSoundResourceRoot = "soundEffect";
    private const string BgmResourceRoot = "Bgm";

    [SerializeField] private int initialPoolSize = 8;
    [SerializeField] private AudioSource bgmSource;

    private readonly Dictionary<EffectSound, AudioClip> _effectClipCache = new Dictionary<EffectSound, AudioClip>();
    private readonly Dictionary<BgmName, AudioClip> _bgmClipCache = new Dictionary<BgmName, AudioClip>();

    private readonly Stack<AudioSource> _effectPool = new Stack<AudioSource>();
    private Transform _effectPoolParent;

    private Coroutine _volumeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _effectPoolParent = new GameObject("EffectSourcePool").transform;
        _effectPoolParent.SetParent(transform);

        if (bgmSource == null)
        {
            GameObject bgmObject = new GameObject("BgmSource");
            bgmObject.transform.SetParent(transform);
            bgmSource = bgmObject.AddComponent<AudioSource>();
        }
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;

        for (int i = 0; i < initialPoolSize; i++)
        {
            _effectPool.Push(CreatePooledSource());
        }
    }

    // ---------- Sound Effect (오브젝트 풀) ----------

    public void Play(EffectSound sound)
    {
        AudioClip clip = GetEffectClip(sound);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] 사운드 이펙트 클립을 찾을 수 없습니다: {EffectSoundResourceRoot}/{sound}");
            return;
        }

        AudioSource source = RentPooledSource();
        source.clip = clip;
        source.Play();
        StartCoroutine(ReturnAfterPlayback(source, clip.length));
    }

    private AudioSource CreatePooledSource()
    {
        GameObject go = new GameObject("EffectSource");
        go.transform.SetParent(_effectPoolParent);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private AudioSource RentPooledSource()
    {
        return _effectPool.Count > 0 ? _effectPool.Pop() : CreatePooledSource();
    }

    private void ReturnPooledSource(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        _effectPool.Push(source);
    }

    private IEnumerator ReturnAfterPlayback(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnPooledSource(source);
    }

    private AudioClip GetEffectClip(EffectSound sound)
    {
        if (_effectClipCache.TryGetValue(sound, out AudioClip cached))
            return cached;

        AudioClip clip = Resources.Load<AudioClip>($"{EffectSoundResourceRoot}/{sound}");
        _effectClipCache[sound] = clip;
        return clip;
    }

    // ---------- BGM (단일 소스) ----------

    public void Play(BgmName name)
    {
        AudioClip clip = GetBgmClip(name);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM 클립을 찾을 수 없습니다: {BgmResourceRoot}/{name}");
            return;
        }

        if (_volumeRoutine != null)
        {
            StopCoroutine(_volumeRoutine);
            _volumeRoutine = null;
        }

        bgmSource.clip = clip;
        bgmSource.volume = 1f;
        bgmSource.Play();
    }

    // 일시정지
    public void Stop()
    {
        bgmSource.Pause();
    }

    // 재개
    public void Resume()
    {
        bgmSource.UnPause();
    }

    // 현재 볼륨을 0으로 만들고 magnitude까지 time초 동안 1차식으로 서서히 올린다.
    public void FadeIn(float magnitude, float time)
    {
        bgmSource.volume = 0f;
        ChangeVolume(magnitude, time);
    }

    // 진행 중인 볼륨 변화가 있으면 취소하고, 현재 볼륨에서 magnitude(0~1)까지 time초 동안 1차식으로 변화시킨다.
    public void ChangeVolume(float magnitude, float time)
    {
        if (time == 0)
        {
            bgmSource.volume = magnitude;
            return;
        }
        magnitude = Mathf.Clamp01(magnitude);

        if (_volumeRoutine != null)
            StopCoroutine(_volumeRoutine);

        _volumeRoutine = StartCoroutine(ChangeVolumeRoutine(magnitude, Mathf.Max(0f, time)));
    }

    private IEnumerator ChangeVolumeRoutine(float targetVolume, float duration)
    {
        float startVolume = bgmSource.volume;

        if (duration <= 0f)
        {
            bgmSource.volume = targetVolume;
            _volumeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = targetVolume;
        _volumeRoutine = null;
    }

    private AudioClip GetBgmClip(BgmName name)
    {
        if (_bgmClipCache.TryGetValue(name, out AudioClip cached))
            return cached;

        AudioClip[] clips = Resources.LoadAll<AudioClip>($"{BgmResourceRoot}/{name}");
        AudioClip clip = clips.Length > 0 ? clips[0] : null;
        _bgmClipCache[name] = clip;
        return clip;
    }
}
