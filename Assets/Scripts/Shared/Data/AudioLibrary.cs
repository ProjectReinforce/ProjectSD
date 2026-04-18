using UnityEngine;

namespace SwDreams.Shared.Data
{
    /// <summary>
    /// 전체 BGM/SFX를 중앙 관리하는 ScriptableObject.
    /// AudioManager에서 참조.
    ///
    /// 셋업:
    ///   Assets/Data/ 폴더에서 Create → SwDreams/AudioLibrary
    ///   인스펙터에서 AudioClip들을 연결.
    ///   없는 항목은 null로 두면 해당 사운드만 무음 처리.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "SwDreams/AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        [Header("BGM")]
        public AudioClip menuBGM;
        public AudioClip gameBGM;
        public AudioClip bossBGM;
        public AudioClip clearBGM;
        public AudioClip gameOverBGM;

        [Header("SFX — 플레이어")]
        public AudioClip playerHit;
        public AudioClip playerDeath;
        public AudioClip playerRespawn;
        public AudioClip levelUp;

        [Header("SFX — 적")]
        public AudioClip enemyDeath;

        [Header("SFX — 보스")]
        public AudioClip bossWarning;
        public AudioClip bossPhaseChange;
        public AudioClip bossDeath;
        public AudioClip bossShockwave;
        public AudioClip bossCircleZone;
        public AudioClip bossExplosion;
        public AudioClip bossGlobalSlow;

        [Header("SFX — 스킬")]
        public AudioClip skillProjectile;
        public AudioClip skillArea;
        public AudioClip skillOrbital;
        public AudioClip skillPlaced;
        public AudioClip skillDebuff;

        [Header("SFX — UI")]
        public AudioClip uiSelect;
        public AudioClip uiConfirm;
        public AudioClip uiCountdown;
        public AudioClip resultShow;
    }
}
