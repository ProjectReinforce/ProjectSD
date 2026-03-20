using UnityEngine;

namespace SwDreams.Data
{
    /// <summary>
    /// 보스 데이터 SO. 3페이즈 스펙 + 멀티 스케일링.
    /// enemy_boss_design.docx 섹션 6 기반.
    ///
    /// 에셋 생성: Create > SwDreams > BossData
    /// BossSpawner Inspector에서 연결.
    /// </summary>
    [CreateAssetMenu(fileName = "BossData", menuName = "SwDreams/BossData")]
    public class BossData : ScriptableObject
    {
        [Header("기본 정보")]
        public string bossName = "드림 이터";
        public Sprite sprite;

        [Header("기본 스펙 (2인 기준)")]
        public int baseHP = 8000;
        public float moveSpeed = 0.40f;
        public int contactDamage = 30;
        public float knockbackForce = 0.80f;

        [Header("페이즈 전환 (체력 비율)")]
        [Range(0f, 1f)] public float phase2Threshold = 0.6f;
        [Range(0f, 1f)] public float phase3Threshold = 0.3f;

        // ===== Phase 1 (100%~60%) =====
        [Header("Phase 1 — 추적 + 충격파")]
        public float p1ShockwaveCooldown = 5f;
        public int p1ShockwaveDamage = 40;
        [Tooltip("부채꼴 반각 (도). 60이면 전방 120도")]
        public float p1ShockwaveHalfAngle = 60f;
        public float p1ShockwaveRange = 4f;

        // ===== Phase 2 (60%~30%) =====
        [Header("Phase 2 — 속도 증가 + 원형 지대")]
        public float p2MoveSpeed = 0.48f;
        public float p2ShockwaveCooldown = 3f;
        public float p2CircleZoneCooldown = 10f;
        public int p2CircleZoneDamage = 60;
        [Tooltip("경고 표시 후 폭발까지 딜레이 (초)")]
        public float p2CircleZoneDelay = 3f;
        public float p2CircleZoneRadius = 2.5f;

        // ===== Phase 3 (30%~0%) =====
        [Header("Phase 3 — 광폭화")]
        public float p3MoveSpeed = 0.56f;
        public int p3ContactDamage = 50;
        public float p3ShockwaveCooldown = 2f;
        [Tooltip("원형 지대 동시 생성 수")]
        public int p3CircleZoneCount = 2;
        [Tooltip("전체 슬로우 발동 간격 (초)")]
        public float p3SlowInterval = 5f;
        [Tooltip("슬로우 지속시간 (초)")]
        public float p3SlowDuration = 3f;
        [Tooltip("슬로우 배율 (0.5 = 50% 감속)")]
        [Range(0f, 1f)] public float p3SlowMultiplier = 0.5f;

        // ===== 멀티플레이어 스케일링 =====
        [Header("멀티 스케일링")]
        [Tooltip("인덱스: 0=1인(0.6x), 1=2인(1.0x), 2=3인(1.4x), 3=4인(1.8x)")]
        public float[] hpMultiplier = { 0.6f, 1.0f, 1.4f, 1.8f };

        // ===== 프리팹 =====
        [Header("프리팹")]
        [Tooltip("보스 프리팹 (Resources 폴더)")]
        public GameObject bossPrefab;
        [Tooltip("충격파 이펙트 프리팹")]
        public GameObject shockwaveEffectPrefab;
        [Tooltip("원형 지대 경고 + 폭발 이펙트 프리팹")]
        public GameObject circleZoneEffectPrefab;
        [Tooltip("슬로우 화면 이펙트 프리팹")]
        public GameObject slowEffectPrefab;

        // ===== 유틸리티 =====

        public float GetHPMultiplier(int playerCount)
        {
            if (hpMultiplier == null || hpMultiplier.Length == 0) return 1f;
            int idx = Mathf.Clamp(playerCount - 1, 0, hpMultiplier.Length - 1);
            return hpMultiplier[idx];
        }

        /// <summary>
        /// 해당 페이즈의 이동속도 반환.
        /// </summary>
        public float GetMoveSpeedForPhase(Domain.BossPhase phase)
        {
            switch (phase)
            {
                case Domain.BossPhase.Phase2: return p2MoveSpeed;
                case Domain.BossPhase.Phase3: return p3MoveSpeed;
                default: return moveSpeed;
            }
        }

        /// <summary>
        /// 해당 페이즈의 접촉 데미지 반환.
        /// </summary>
        public int GetContactDamageForPhase(Domain.BossPhase phase)
        {
            switch (phase)
            {
                case Domain.BossPhase.Phase3: return p3ContactDamage;
                default: return contactDamage;
            }
        }
    }
}