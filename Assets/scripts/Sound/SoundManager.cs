using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio; // [추가 - 설정 음량]

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

    // Bgm/{이름}/ 폴더 관례를 안 쓰고 Sound/{카테고리}/ 밑에 이미 있는 클립을 그대로 BGM으로
    // 재생하고 싶은 항목의 전체 Resources 경로. 여기 없는 BgmName은 기존처럼
    // Resources/Bgm/{이름}/ 폴더에서 찾는다(GetBgmClip 참고).
    private static readonly Dictionary<BgmName, string> BgmResourcePathOverride = new Dictionary<BgmName, string>
    {
        { BgmName.BattleBGM, "Sound/Battle/BattleBGM" },
        { BgmName.Menu, "Sound/Misc/menu" },
    };

    [SerializeField] private int initialPoolSize = 8;
    [SerializeField] private AudioSource bgmSource;


    // =========================================================
    // [추가 - 설정 음량]
    // 기존 AudioSource.volume과 별도로 설정창의 음량을 적용한다.
    // =========================================================

    [Header("Setting Volume")]

    [SerializeField]
    private AudioMixerGroup bgmMixerGroup;
    // [추가 - 설정 음량] BGM이 통과할 AudioMixer Group

    [SerializeField]
    private AudioMixerGroup effectMixerGroup;
    // [추가 - 설정 음량] 효과음이 통과할 AudioMixer Group

    [SerializeField, Range(0, 30)]
    private int bgmSettingVolume = 30;
    // [추가 - 설정 음량] BGM 설정 음량 0~30

    [SerializeField, Range(0, 30)]
    private int effectSettingVolume = 30;
    // [추가 - 설정 음량] 효과음 설정 음량 0~30

    private const string BgmVolumeParameter = "BGMVolume";
    // [추가 - 설정 음량] AudioMixer Exposed Parameter 이름

    private const string EffectVolumeParameter = "EffectVolume";
    // [추가 - 설정 음량] AudioMixer Exposed Parameter 이름


    private readonly Dictionary<EffectSound, AudioClip> _effectClipCache = new Dictionary<EffectSound, AudioClip>();
    private readonly Dictionary<BgmName, AudioClip> _bgmClipCache = new Dictionary<BgmName, AudioClip>();

    private readonly Stack<AudioSource> _effectPool = new Stack<AudioSource>();
    private Transform _effectPoolParent;

    private Coroutine _volumeRoutine;
    private Coroutine _delayedBgmRoutine;

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


        // [추가 - 설정 음량]
        // 기존 BGM AudioSource가 BGM Mixer를 통과하도록 연결한다.
        if (bgmMixerGroup != null)
        {
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;
        }


        for (int i = 0; i < initialPoolSize; i++)
        {
            _effectPool.Push(CreatePooledSource());
        }


        // [추가 - 설정 음량]
        // 게임 시작 시 현재 설정되어 있는 0~30 값을 적용한다.
        ApplyBgmSettingVolume();
        ApplyEffectSettingVolume();


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
            Debug.LogWarning(
                $"[SoundManager] 사운드 이펙트 클립을 찾을 수 없습니다: {EffectSoundResourceRoot}/{GetEffectSoundPath(sound)}"
            );

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


        // [추가 - 설정 음량]
        // 모든 효과음 AudioSource가 Effect Mixer를 통과하도록 한다.
        if (effectMixerGroup != null)
        {
            source.outputAudioMixerGroup = effectMixerGroup;
        }


        return source;
    }

    private AudioSource RentPooledSource()
    {
        return _effectPool.Count > 0
            ? _effectPool.Pop()
            : CreatePooledSource();
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

        AudioClip clip =
            Resources.Load<AudioClip>(
                $"{EffectSoundResourceRoot}/{GetEffectSoundPath(sound)}"
            );

        _effectClipCache[sound] = clip;

        return clip;
    }

    private static string GetEffectSoundPath(EffectSound sound)
    {
        return EffectSoundCategory.TryGetValue(sound, out string category)
            ? $"{category}/{sound}"
            : sound.ToString();
    }

    // 이펙트 사운드 클립의 재생 길이(초). "이 효과음이 끝난 뒤" 다음 동작(예: BGM 전환)을
    // 예약하고 싶을 때 씀 (PlayBgmDelayed와 함께 사용).
    public float GetEffectClipLength(EffectSound sound)
    {
        AudioClip clip = GetEffectClip(sound);

        return clip != null
            ? clip.length
            : 0f;
    }

    // ---------- BGM (단일 소스) ----------

    public void Play(BgmName name)
    {
        AudioClip clip = GetBgmClip(name);

        if (clip == null)
        {
            Debug.LogWarning(
                $"[SoundManager] BGM 클립을 찾을 수 없습니다: {name}"
            );

            return;
        }

        if (_delayedBgmRoutine != null)
        {
            StopCoroutine(_delayedBgmRoutine);
            _delayedBgmRoutine = null;
        }

        if (_volumeRoutine != null)
        {
            StopCoroutine(_volumeRoutine);
            _volumeRoutine = null;
        }

        bgmSource.clip = clip;

        // 기존 기능 그대로
        bgmSource.volume = 1f;

        bgmSource.Play();
    }

    // delaySeconds초 뒤에 Play(name)을 예약한다.
    public void PlayBgmDelayed(BgmName name, float delaySeconds)
    {
        if (_delayedBgmRoutine != null)
            StopCoroutine(_delayedBgmRoutine);

        _delayedBgmRoutine =
            StartCoroutine(
                PlayBgmAfterDelay(name, delaySeconds)
            );
    }

    private IEnumerator PlayBgmAfterDelay(
        BgmName name,
        float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        _delayedBgmRoutine = null;

        Play(name);
    }

    // 지금 재생 중인 BGM을 완전히 멈춘다.
    public void StopBgm()
    {
        if (_delayedBgmRoutine != null)
        {
            StopCoroutine(_delayedBgmRoutine);
            _delayedBgmRoutine = null;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
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

    // 진행 중인 볼륨 변화가 있으면 취소하고,
    // 현재 볼륨에서 magnitude까지 변화시킨다.
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

        _volumeRoutine =
            StartCoroutine(
                ChangeVolumeRoutine(
                    magnitude,
                    Mathf.Max(0f, time)
                )
            );
    }

    private IEnumerator ChangeVolumeRoutine(
        float targetVolume,
        float duration)
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

            bgmSource.volume =
                Mathf.Lerp(
                    startVolume,
                    targetVolume,
                    elapsed / duration
                );

            yield return null;
        }

        bgmSource.volume = targetVolume;
        _volumeRoutine = null;
    }

    private AudioClip GetBgmClip(BgmName name)
    {
        if (_bgmClipCache.TryGetValue(name, out AudioClip cached))
            return cached;

        AudioClip clip;

        if (BgmResourcePathOverride.TryGetValue(
            name,
            out string overridePath))
        {
            clip = Resources.Load<AudioClip>(overridePath);
        }
        else
        {
            AudioClip[] clips =
                Resources.LoadAll<AudioClip>(
                    $"{BgmResourceRoot}/{name}"
                );

            clip = clips.Length > 0
                ? clips[0]
                : null;
        }

        _bgmClipCache[name] = clip;

        return clip;
    }


    // =========================================================
    // [추가 - 설정 음량]
    // SettingUIPanel에서 호출하는 부분
    // =========================================================


    // [추가 - 설정 음량]
    // BGM 음량을 0~30으로 설정한다.
    public void SetBgmVolume(int volume)
    {
        bgmSettingVolume =
            Mathf.Clamp(volume, 0, 30);

        ApplyBgmSettingVolume();
    }


    // [추가 - 설정 음량]
    // 효과음 음량을 0~30으로 설정한다.
    public void SetEffectVolume(int volume)
    {
        effectSettingVolume =
            Mathf.Clamp(volume, 0, 30);

        ApplyEffectSettingVolume();
    }


    // [추가 - 설정 음량]
    public int GetBgmVolume()
    {
        return bgmSettingVolume;
    }


    // [추가 - 설정 음량]
    public int GetEffectVolume()
    {
        return effectSettingVolume;
    }


    // [추가 - 설정 음량]
    private void ApplyBgmSettingVolume()
    {
        if (bgmMixerGroup == null)
            return;

        float db =
            VolumeLevelToDecibel(
                bgmSettingVolume
            );

        bgmMixerGroup.audioMixer.SetFloat(
            BgmVolumeParameter,
            db
        );
    }


    // [추가 - 설정 음량]
    private void ApplyEffectSettingVolume()
    {
        if (effectMixerGroup == null)
            return;

        float db =
            VolumeLevelToDecibel(
                effectSettingVolume
            );

        effectMixerGroup.audioMixer.SetFloat(
            EffectVolumeParameter,
            db
        );
    }


    // [추가 - 설정 음량]
    //
    // 설정값:
    //
    // 30 = 최대 볼륨
    // 15 = 약 절반
    // 0  = 음소거
    //
    // AudioMixer가 사용하는 dB 값으로 변환한다.
    private static float VolumeLevelToDecibel(int volume)
    {
        volume = Mathf.Clamp(volume, 0, 30);

        if (volume == 0)
            return -80f;

        float normalized =
            volume / 30f;

        return Mathf.Log10(normalized) * 20f;
    }
}