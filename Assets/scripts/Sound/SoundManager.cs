using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private const string EffectSoundResourceRoot = "Sound";
    private const string BgmResourceRoot = "Bgm";

    // Resources/Sound/{카테고리}/{EffectSound 이름} 구조를 위한 EffectSound → 하위 폴더 매핑.
    // EffectSound.cs에 새 항목을 추가하면 여기에도 카테고리를 등록해야 한다.
    private static readonly Dictionary<EffectSound, string> EffectSoundCategory = new Dictionary<EffectSound, string>
    {
        { EffectSound.Attack, "Attack" },
        { EffectSound.UpcomingAttack, "Attack" },
        { EffectSound.DamageWeak, "Attack" },
        { EffectSound.Damage, "Attack" },
        { EffectSound.DamageBig, "Attack" },
        { EffectSound.DamageGuarded, "Attack" },
        { EffectSound.DamageShielded, "Attack" },

        { EffectSound.BattleStart, "Battle" },
        { EffectSound.TurnEnd1, "Battle" },
        { EffectSound.TurnEnd2, "Battle" },
        { EffectSound.PlayerWin, "Battle" },

        { EffectSound.Select, "Card" },
        { EffectSound.Unselect, "Card" },
        { EffectSound.MoveSelect1, "Card" },
        { EffectSound.MoveSelect2, "Card" },
        { EffectSound.UseCard, "Card" },
        { EffectSound.PlayCard, "Card" },

        { EffectSound.Buff, "Effect" },
        { EffectSound.Debuff, "Effect" },
        { EffectSound.Burn, "Effect" },
        { EffectSound.ShieldGet, "Effect" },
        { EffectSound.TimeSkip, "Effect" },
    };

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

        PreloadEffectClips();
    }

    // 이펙트 사운드 클립들은 preloadAudioData가 꺼져 있어(용량 절약을 위해 Resources 폴더 전체를
    // 한 번에 임포트할 때 기본값 그대로 둔 것으로 보인다) 실제 오디오 데이터가 Resources.Load
    // 시점이 아니라 "그 클립을 처음 Play한 시점"에야 로드된다 — 그래서 이동/선택처럼 자주 쓰이는
    // 효과음일수록 첫 재생에서 커서를 움직인 것과 소리가 나는 시점 사이에 눈에 띄는 지연이 생긴다.
    // 여기서 미리 한 번씩 로드해 그 지연을 게임 시작 시점으로 옮겨둔다.
    private void PreloadEffectClips()
    {
        foreach (EffectSound sound in Enum.GetValues(typeof(EffectSound)))
        {
            AudioClip clip = GetEffectClip(sound);
            clip?.LoadAudioData();
        }
    }

    // ---------- Sound Effect (오브젝트 풀) ----------

    public void Play(EffectSound sound)
    {
        AudioClip clip = GetEffectClip(sound);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] 사운드 이펙트 클립을 찾을 수 없습니다: {EffectSoundResourceRoot}/{GetEffectSoundPath(sound)}");
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

        AudioClip clip = Resources.Load<AudioClip>($"{EffectSoundResourceRoot}/{GetEffectSoundPath(sound)}");
        _effectClipCache[sound] = clip;
        return clip;
    }

    private static string GetEffectSoundPath(EffectSound sound)
    {
        return EffectSoundCategory.TryGetValue(sound, out string category)
            ? $"{category}/{sound}"
            : sound.ToString();
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
