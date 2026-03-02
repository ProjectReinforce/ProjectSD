using UnityEngine;

namespace SwDreams.Data
{
    /// <summary>
    /// 게임플레이 상수 중앙 관리 SO.
    /// 코드에 하드코딩된 매직 넘버를 Inspector에서 조정 가능하도록 분리.
    ///
    /// 사용법:
    /// 1. Project 창에서 Create > SwDreams > GameplayConfig 생성
    /// 2. 수치 설정 후 필요한 Manager/Entity에 연결
    ///
    /// Phase 5 선행 개선:
    /// - 기존 코드의 하드코딩 상수를 이 SO로 점진적 이동
    /// - 각 컴포넌트에서 [SerializeField]로 참조하여 사용
    ///
    /// 향후 확장:
    /// - Phase 6: 보스 관련 설정 추가
    /// - Phase 7: 밸런싱 프리셋 지원 (Easy/Normal/Hard)
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayConfig", menuName = "SwDreams/GameplayConfig")]
    public class GameplayConfig : ScriptableObject
    {
        // ===== 경험치 오브 =====
        [Header("경험치 오브")]
        [Tooltip("자석 흡수 시작 범위 (단위: Unity unit)")]
        public float magnetRange = 5f;

        [Tooltip("자석 흡수 이동 속도")]
        public float magnetSpeed = 8f;

        // ===== 스킬 시스템 =====
        [Header("스킬 시스템")]
        [Tooltip("플레이어당 최대 스킬 슬롯 수 (액티브 + 패시브 합계)")]
        public int maxSkillSlots = 6;

        [Tooltip("다중 투사체 발사 시 탄 사이 각도 (도)")]
        public float projectileSpreadAngle = 15f;

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
        public int[] chaosLevels = { 5, 10, 15 };

        // ===== 게임 진행 =====
        [Header("게임 진행")]
        [Tooltip("총 게임 시간 (초). 10분 = 600초")]
        public float totalGameTime = 600f;

        // TODO Phase 6: 보스 관련 설정 추가
        // public float bossSpawnTime = 300f;
        // public float bossWarningDuration = 3f;

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