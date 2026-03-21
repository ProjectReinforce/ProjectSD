using UnityEngine;

namespace SwDreams.Data
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

        // ===== 스킬 시스템 =====
        [Header("스킬 시스템")]
        [Tooltip("플레이어당 최대 스킬 슬롯 수 (액티브 + 패시브 합계)")]
        public int maxSkillSlots = 6;

        [Tooltip("다중 투사체 발사 시 탄 사이 각도 (도)")]
        public float projectileSpreadAngle = 15f;

        [Tooltip("투사체 기본 넉백 힘 (PlayerStats.KnockbackMultiplier와 곱셈)")]
        public float baseKnockbackForce = 2f;

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

        // ===== 게임 진행 =====
        [Header("게임 진행")]
        [Tooltip("총 게임 시간 (초). 15분 = 900초")]
        public float totalGameTime = 900f;
        
        // ===== 보스 =====
        [Header("보스")]
        [Tooltip("보스 등장 시간 (초). 15분 = 900초")]
        public float bossSpawnTime = 900f;

        [Tooltip("보스 등장 경고 연출 시간 (초)")]
        public float bossWarningDuration = 3f;

        // ===== 사망/부활 =====
        [Header("사망/부활")]
        [Tooltip("사망 후 부활까지 대기 시간 (초)")]
        public float respawnDelay = 10f;

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