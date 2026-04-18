using UnityEngine;
using SwDreams.Adapter.Manager;
using SwDreams.Domain;
using SwDreams.Shared.Domain;
using SwDreams.Adapter.Entity;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// GameScene 사운드 연결자.
    /// 기존 이벤트에 구독하여 AudioManager.PlaySFX/PlayBGM 호출.
    /// 기존 코드 수정 없이 사운드 추가 (OCP).
    ///
    /// 구독 대상:
    ///   GameManager.OnStateChanged → BGM 전환, 결과 SFX
    ///   GameManager.OnLevelUp → 레벨업 SFX
    ///   BossSpawner.CurrentBoss → 보스 이벤트 (OnPhaseChanged, OnDied)
    ///   SpawnManager 경유 적 사망 → 적 사망 SFX (쓰로틀링)
    ///
    /// 셋업: GameScene에 빈 오브젝트 → GameAudioConnector 부착.
    /// </summary>
    public class GameAudioConnector : MonoBehaviour
    {
        // 적 사망 SFX 쓰로틀링 (대량 동시 사망 시 소리 폭발 방지)
        private float enemyDeathSfxCooldown = 0.05f;
        private float enemyDeathSfxTimer = 0f;

        private Boss trackedBoss;

        private void Start()
        {
            // GameManager 이벤트
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += OnGameStateChanged;
                GameManager.Instance.OnLevelUp += OnLevelUp;
            }

            // 게임 BGM 시작
            AudioManager.Instance?.PlayGameBGM();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= OnGameStateChanged;
                GameManager.Instance.OnLevelUp -= OnLevelUp;
            }

            DetachFromBoss();
        }

        private void Update()
        {
            // 적 사망 SFX 쓰로틀링 타이머
            if (enemyDeathSfxTimer > 0f)
                enemyDeathSfxTimer -= Time.deltaTime;

            // 보스 등장 감지 (폴링)
            if (trackedBoss == null && BossSpawner.Instance?.CurrentBoss != null)
                AttachToBoss(BossSpawner.Instance.CurrentBoss);
        }

        // ===== GameState 전환 =====

        private void OnGameStateChanged(GameManager.GameState newState)
        {
            var lib = AudioManager.Instance?.Library;
            if (lib == null) return;

            switch (newState)
            {
                case GameManager.GameState.BossFight:
                    AudioManager.Instance.PlayBossBGM();
                    AudioManager.Instance.PlaySFX(lib.bossWarning);
                    break;

                case GameManager.GameState.GameClear:
                    AudioManager.Instance.PlayClearBGM();
                    AudioManager.Instance.PlaySFX(lib.resultShow);
                    break;

                case GameManager.GameState.GameOver:
                    AudioManager.Instance.PlayGameOverBGM();
                    AudioManager.Instance.PlaySFX(lib.resultShow);
                    break;
            }
        }

        // ===== 레벨업 =====

        private void OnLevelUp(int level)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.Library?.levelUp);
        }

        // ===== 보스 이벤트 =====

        private void AttachToBoss(Boss boss)
        {
            trackedBoss = boss;
            boss.OnPhaseChanged += OnBossPhaseChanged;
            boss.OnDied += OnBossDied;
        }

        private void DetachFromBoss()
        {
            if (trackedBoss != null)
            {
                trackedBoss.OnPhaseChanged -= OnBossPhaseChanged;
                trackedBoss.OnDied -= OnBossDied;
                trackedBoss = null;
            }
        }

        private void OnBossPhaseChanged(BossPhase phase)
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.Library?.bossPhaseChange);
        }

        private void OnBossDied()
        {
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.Library?.bossDeath);
        }

        // ===== 적 사망 (외부에서 호출) =====

        /// <summary>
        /// SpawnManager.OnEnemyDied에서 호출하거나,
        /// 별도 이벤트 구독으로 연결.
        /// 쓰로틀링 적용 (0.05초 간격).
        /// </summary>
        public void OnEnemyDied()
        {
            if (enemyDeathSfxTimer > 0f) return;
            enemyDeathSfxTimer = enemyDeathSfxCooldown;
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.Library?.enemyDeath);
        }

        // ===== 싱글톤 접근 (SpawnManager에서 호출용) =====
        public static GameAudioConnector Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }
    }
}
