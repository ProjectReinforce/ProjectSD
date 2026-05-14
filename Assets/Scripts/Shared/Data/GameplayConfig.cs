using UnityEngine;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Character.Adapter;
using SwDreams.Shared.Domain;

namespace SwDreams.Shared.Data
{
    /// <summary>
    /// 게임플레이 상수 중앙 관리 SO.
    /// 코드에 하드코딩된 매직 넘버를 Inspector에서 조정 가능하도록 분리.
    ///
    /// 소유자: GameManager ([SerializeField]로 연결)
    /// 접근: GameManager.Instance.Config로 다른 매니저/엔티티에서 읽기 전용 접근
    ///
    /// 사용법:
    /// 1. Project 창에서 Create > SwDreams > GameplayConfig 생성
    /// 2. 수치 설정
    /// 3. GameManager Inspector의 Config 필드에 연결 (한 곳에서만!)
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "SwDreams/GameplayConfig")]
    public class GameplayConfig : ScriptableObject
    {
        // ===== 경험치 오브 =====
        [Header("경험치 오브")]
        [Tooltip("자석 흡수 시작 범위 (단위: Unity unit)")]
        public float magnetRange = 0.8f;

        [Tooltip("자석 흡수 이동 속도")]
        public float magnetSpeed = 1.3f;

        [Tooltip("월드에 동시 존재 가능한 경험치 오브 최대 수. 상한 도달 시 새 오브 드랍 생략(프레임 드랍 방지). 0 이하면 무제한")]
        public int maxActiveExpOrbs = 200;

        [Tooltip("적 사망 위치를 중심으로 드랍 아이템을 흩뿌리는 반경(unit). 0 이면 사망 위치 정확히 스폰. 기본 0.5.")]
        public float dropScatterRadius = 0.5f;

        // ===== 드랍 / 등급 시스템 (Phase 0 인프라) =====
        [Header("등급 가중치 (Rarity enum 순서: Common/Rare/Epic/Legendary)")]
        [Tooltip("혼돈 스킬 / 능력치 부스트 선택지 공용 fallback 가중치. 개별 SO 에 명시된 가중치가 있으면 그것이 우선.")]
        public float[] defaultRarityWeights = { 60f, 25f, 12f, 3f };

        // ===== 스킬 시스템 =====
        [Header("스킬 시스템")]
        [Tooltip("플레이어당 최대 스킬 슬롯 수 (액티브 + 패시브 합계)")]
        public int maxSkillSlots = 6;

        [Tooltip("한 게임당 일반 스킬 선택지 새로고침 기본 횟수. 혼돈 스킬로 +N 가산 가능.")]
        public int baseSkillRefreshCharges = 2;

        [Tooltip("다중 투사체 발사 시 탄 사이 각도 (도)")]
        public float projectileSpreadAngle = 15f;

        [Tooltip("투사체 기본 넉백 힘 (PlayerStats.KnockbackMultiplier와 곱셈)")]
        public float baseKnockbackForce = 0.9f;

        // ===== 치명타 (damage-formula.md § 9·10) =====
        [Header("치명타")]
        [Tooltip("치명타 데미지 기본 배율. 1.5 = 1.5배. PlayerStats.baseCritDamage 기본값과 일치 권장.")]
        public float critMultBase = 1.5f;

        [Tooltip("치명타 확률 기본값 (0~1). 0.05 = 5%. CharacterData.critChance / PlayerStats.baseCritChance 기본값과 일치 권장.")]
        [Range(0f, 1f)]
        public float critChanceBase = 0.05f;

        // ===== 비주얼 피드백 =====
        [Header("비주얼 피드백")]
        [Tooltip("데미지 숫자 팝업 프리팹 (TextMeshPro + DamagePopup)")]
        public GameObject damagePopupPrefab;

        [Tooltip("피격 파티클 프리팹 (ParticleSystem)")]
        public GameObject hitEffectPrefab;

        // ===== 레벨업 =====
        [Header("레벨업")]
        [Tooltip("선택 제한시간 (초). 초과 시 랜덤 자동 선택")]
        public float selectionTimeout = 15f;

        [Tooltip("레벨업 시 제시되는 선택지 수")]
        public int choiceCount = 3;

        [Tooltip("진화 선택지 등장 확률 (0~1)")]
        [Range(0f, 1f)]
        public float evolutionChance = 0.7f;

        [Tooltip("혼돈 스킬이 등장하는 레벨 목록")]
        public int[] chaosLevels = { 10, 20, 30 };

        // ===== 난이도 배율 =====
        [Header("난이도 배율 (DifficultyData 곡선 전체에 곱셈 적용)")]
        [Tooltip("쉬움 — 적 HP/데미지/이속/최대 동시 수에 일괄 곱. 0.7 권장 (느슨한 진행).")]
        [Range(0.1f, 3f)] public float difficultyMultiplierEasy = 0.7f;

        [Tooltip("보통 — 기본값. 1.0 권장 (DifficultyData 그대로).")]
        [Range(0.1f, 3f)] public float difficultyMultiplierNormal = 1.0f;

        [Tooltip("어려움 — 적 HP/데미지/이속/최대 동시 수에 일괄 곱. 1.35 권장.")]
        [Range(0.1f, 3f)] public float difficultyMultiplierHard = 1.35f;

        /// <summary>Difficulty enum → 배율 매핑. 알 수 없는 값은 Normal 폴백.</summary>
        public float GetDifficultyMultiplier(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return difficultyMultiplierEasy;
                case Difficulty.Hard: return difficultyMultiplierHard;
                default: return difficultyMultiplierNormal;
            }
        }

        // ===== 보스 =====
        [Header("보스")]
        [Tooltip("보스 등장 시간 (초). 15분 = 900초.\n" +
                 "이 값은 동시에 난이도 곡선(DifficultyData)의 정규화 기준(t=1.0)으로도 사용된다.\n" +
                 "즉 이 값을 줄이면 적 HP·스폰량 상승 곡선도 그만큼 빠르게 진행된다.\n" +
                 "HUD 카운트다운, 호스트 마이그레이션 보스 재트리거 판단도 이 값을 따른다.")]
        public float bossSpawnTime = 900f;

        [Tooltip("보스 등장 경고 연출 시간 (초)")]
        public float bossWarningDuration = 3f;

        // ===== 사망/부활 =====
        [Header("사망/부활")]
        [Tooltip("사망 후 부활까지 대기 시간 (초)")]
        public float respawnDelay = 10f;

        [Tooltip("게임오버/클리어 시 결과창 표시까지 대기 시간 (초). 사망/클리어 애니메이션 클립 길이에 맞춰 조정. 0 이면 즉시 표시.")]
        public float resultPanelDelay = 1.5f;

        [Tooltip("부활 시 HP 비율 (0.5 = 50%)")]
        [Range(0f, 1f)]
        public float respawnHPRatio = 0.5f;

        // ===== 호스트 이탈 =====
        [Header("호스트 이탈")]
        [Tooltip("호스트 재연결 대기 시간 (초)")]
        public float reconnectWaitTime = 5f;

        [Tooltip("비상 보스전 시 보스 약화 기준 (이 비율 이전이면 약화)")]
        [Range(0f, 1f)]
        public float emergencyBossHPRatio = 0.7f;

        // ===== 오브젝트 풀링 =====
        [Header("오브젝트 풀링")]
        [Tooltip("투사체 프리웜 개수 (프리팹당)")]
        public int projectilePrewarmCount = 20;

        [Tooltip("경험치 오브 프리웜 개수")]
        public int expOrbPrewarmCount = 50;

        // ===== 유틸리티 메서드 =====

        /// <summary>
        /// 해당 레벨이 혼돈 스킬 선택 레벨인지 판별.
        /// LevelUpManager.OnTeamLevelUp()에서 사용.
        /// </summary>
        public bool IsChaosLevel(int level)
        {
            if (chaosLevels == null) return false;
            for (int i = 0; i < chaosLevels.Length; i++)
            {
                if (chaosLevels[i] == level) return true;
            }
            return false;
        }
    }
}