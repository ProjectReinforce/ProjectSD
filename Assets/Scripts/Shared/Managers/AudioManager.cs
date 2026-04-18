using UnityEngine;
using DG.Tweening;
using SwDreams.Shared.Data;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// BGM/SFX 재생 관리. DontDestroyOnLoad 싱글톤.
    /// class_design.docx 설계 기반.
    ///
    /// BGM: DOTween 크로스페이드. 한 번에 하나만 재생.
    /// SFX: PlayOneShot. 동시 다중 재생 가능.
    ///
    /// AudioClip은 AudioLibrary SO에서 중앙 관리.
    /// null인 클립은 무음 처리 (에러 없음).
    ///
    /// 셋업:
    ///   MenuScene에 빈 오브젝트 → AudioManager 부착.
    ///   Inspector에서 AudioLibrary SO 연결.
    ///   AudioSource 2개는 자동 생성됨.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioLibrary library;

        [Header("볼륨")]
        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 0.7f;

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

            // AudioSource 자동 생성
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = bgmVolume;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = sfxVolume;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===== BGM =====

        /// <summary>
        /// BGM 크로스페이드 전환. clip이 null이면 페이드아웃만.
        /// </summary>
        public void PlayBGM(AudioClip clip, float fadeTime = 1f)
        {
            if (clip == null)
            {
                StopBGM(fadeTime);
                return;
            }

            // 같은 곡이면 무시
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.DOKill();
            bgmSource.DOFade(0f, fadeTime * 0.5f).OnComplete(() =>
            {
                bgmSource.clip = clip;
                bgmSource.Play();
                bgmSource.DOFade(bgmVolume, fadeTime * 0.5f);
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
        /// </summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // ===== 편의 메서드 =====

        public void PlayMenuBGM() => PlayBGM(library?.menuBGM);
        public void PlayGameBGM() => PlayBGM(library?.gameBGM);
        public void PlayBossBGM() => PlayBGM(library?.bossBGM);
        public void PlayClearBGM() => PlayBGM(library?.clearBGM);
        public void PlayGameOverBGM() => PlayBGM(library?.gameOverBGM);

        // ===== 볼륨 조절 =====

        public void SetBGMVolume(float vol)
        {
            bgmVolume = Mathf.Clamp01(vol);
            bgmSource.volume = bgmVolume;
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            sfxSource.volume = sfxVolume;
        }
    }
}
