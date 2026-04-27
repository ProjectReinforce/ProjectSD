using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;
using SwDreams.Shared.Data;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// BGM/SFX 재생 관리. DontDestroyOnLoad 싱글톤.
    ///
    /// R12 Phase 1: AudioMixer 라우팅 도입.
    /// - 모든 AudioSource 는 BGM/SFX/Voice Group 으로 라우팅
    /// - 볼륨 SSOT = Mixer Exposed Parameter (dB)
    /// - 슬라이더(0~1, Voice 는 0~2) 는 Log10*20 으로 dB 변환
    ///
    /// BGM: DOTween 크로스페이드 (source.volume 페이드, Mixer 볼륨은 유지).
    /// SFX: PlayOneShot. 동시 다중 재생 가능.
    ///
    /// 셋업:
    ///   MasterMixer.mixer + Master/BGM/SFX/Voice 그룹 + Exposed Param (MasterVol/BGMVol/SFXVol/VoiceGain)
    ///   AudioManager 인스펙터에 mixer + bgmGroup + sfxGroup 할당
    ///   AudioLibrary SO 연결
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioLibrary library;

        [Header("Mixer (R12 Phase 1)")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("초기 볼륨 (0~1, Voice 0~2)")]
        [Tooltip("Phase 2 SettingsManager 도입 후 PlayerPrefs 가 우선. 현재는 Awake 에서 적용.")]
        [Range(0f, 1f)]
        [SerializeField] private float initialMasterVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float initialBgmVolume = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float initialSfxVolume = 0.7f;
        [Range(0f, 2f)]
        [SerializeField] private float initialVoiceGain = 1f;

        private const string MasterParam = "MasterVol";
        private const string BgmParam = "BGMVol";
        private const string SfxParam = "SFXVol";
        private const string VoiceParam = "VoiceGain";

        private const float MinDb = -80f;
        private const float MinLinear = 0.0001f;

        private AudioSource bgmSource;
        private AudioSource sfxSource;

        public AudioLibrary Library => library;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            WarnIfMixerMissing();

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = 1f;
            bgmSource.outputAudioMixerGroup = bgmGroup;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 1f;
            sfxSource.outputAudioMixerGroup = sfxGroup;
        }

        private void Start()
        {
            // AudioMixer.SetFloat 가 Awake 시점엔 silently 실패할 수 있음 (Unity 알려진 함정).
            // Mixer 가 running 상태가 된 Start 에서 적용.
            ApplyInitialVolumes();
        }

        private void WarnIfMixerMissing()
        {
            if (mixer == null)
                Debug.LogError("[AudioManager] Mixer 자산이 인스펙터에 할당되지 않음. 볼륨 조절이 동작하지 않습니다.", this);
            if (bgmGroup == null)
                Debug.LogError("[AudioManager] bgmGroup 미할당. BGM 이 Mixer 를 우회합니다.", this);
            if (sfxGroup == null)
                Debug.LogError("[AudioManager] sfxGroup 미할당. SFX 가 Mixer 를 우회합니다.", this);
        }

        private void ApplyInitialVolumes()
        {
            SetMasterVolume(initialMasterVolume);
            SetBGMVolume(initialBgmVolume);
            SetSFXVolume(initialSfxVolume);
            SetVoiceGain(initialVoiceGain);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 에디터 인스펙터 값 변경 시 라이브 적용 (검증 편의).
        /// 플레이 모드에서만 동작. SettingsManager 가 PlayerPrefs 로 ApplyAudio 호출하면 다시 덮어씀.
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (mixer == null) return;
            ApplyInitialVolumes();
        }

        // ===== BGM =====

        /// <summary>
        /// BGM 크로스페이드 전환. clip이 null이면 페이드아웃만.
        /// source.volume 페이드 — Mixer BGMVol 은 사용자 설정값 유지.
        /// </summary>
        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (clip == null)
            {
                StopBGM(fadeTime);
                return;
            }

            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.DOKill();
            bgmSource.DOFade(0f, fadeTime * 0.5f).OnComplete(() =>
            {
                bgmSource.clip = clip;
                bgmSource.Play();
                bgmSource.DOFade(1f, fadeTime * 0.5f);
            });
        }

        public void StopBGM(float fadeTime = 1f)
        {
            bgmSource.DOKill();
            bgmSource.DOFade(0f, fadeTime).OnComplete(() =>
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            });
        }

        // ===== SFX =====

        /// <summary>
        /// SFX 1회 재생. clip이 null이면 무시.
        /// 볼륨은 Mixer SFXVol 이 결정 — PlayOneShot 의 volumeScale 인자 미사용.
        /// </summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            sfxSource.PlayOneShot(clip);
        }

        // ===== 편의 메서드 =====

        public void PlayMenuBGM() => PlayBGM(library?.menuBGM);
        public void PlayGameBGM() => PlayBGM(library?.gameBGM);
        public void PlayBossBGM() => PlayBGM(library?.bossBGM);
        public void PlayClearBGM() => PlayBGM(library?.clearBGM);
        public void PlayGameOverBGM() => PlayBGM(library?.gameOverBGM);

        // ===== 볼륨 조절 (Mixer SetFloat dB) =====

        public void SetMasterVolume(float linear01) => SetMixerVolume(MasterParam, Mathf.Clamp01(linear01));
        public void SetBGMVolume(float linear01) => SetMixerVolume(BgmParam, Mathf.Clamp01(linear01));
        public void SetSFXVolume(float linear01) => SetMixerVolume(SfxParam, Mathf.Clamp01(linear01));

        /// <summary>
        /// Voice 만 0~2 범위 (1.0 = 0dB, 2.0 = +6dB 부스트).
        /// Phase 8-2 Photon Voice 2 통합 시 Speaker AudioSource 가 Voice Group 으로 라우팅됨.
        /// </summary>
        public void SetVoiceGain(float linear02) => SetMixerVolume(VoiceParam, Mathf.Clamp(linear02, 0f, 2f));

        private void SetMixerVolume(string param, float linear)
        {
            if (mixer == null) return;
            mixer.SetFloat(param, LinearToDb(linear));
        }

        private static float LinearToDb(float linear)
        {
            return linear <= MinLinear ? MinDb : Mathf.Log10(linear) * 20f;
        }
    }
}
