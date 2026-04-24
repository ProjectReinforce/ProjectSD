using System;
using UnityEngine;

namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// 혼돈 스킬 효과가 게임 이벤트에 훅하기 위한 중앙 이벤트 버스 포트.
    /// ChaosSkillManager 가 구현. 각 <see cref="IChaosEffectHandler"/> 는 관심있는 이벤트에 구독.
    ///
    /// 의도:
    /// - SpawnManager, PlayerHealth, LevelUpManager 등이 ChaosSkillManager 의 구체 메서드
    ///   (OnEnemyKilled 등) 대신 이 포트로만 이벤트 발행.
    /// - 혼돈 효과 추가 시 이 포트만 구독하면 되므로 switch / 하드코딩 없음.
    ///
    /// 구현된 이벤트:
    ///   OnEnemyKilled      : SpawnManager.OnEnemyDied (호스트) / 원격 클라의 비주얼 표시.
    ///   OnPlayerTakeDamage : PlayerHealth.TakeDamage 직후 (피격 데미지).
    ///   OnPlayerDeath      : PlayerHealth.Die().
    ///   OnLevelUpChoice    : LevelUpManager.SendNormalChoices 전 (Gambler 등 시스템 개조).
    ///
    /// 미구현 혼돈 스킬 추가 시 필요 훅 (Sprint/TimeWarp/Mirror/Executioner/Harvester/Scapegoat 등)
    /// 은 docs/game-design/skills/chaos/ 참조 → 이 인터페이스 확장.
    /// </summary>
    public interface IChaosHookBus
    {
        /// <summary>
        /// 적 처치 시 발행. isVisualOnly=true 면 클라이언트 비주얼 전용 (호스트 데미지 로직 스킵).
        /// </summary>
        event Action<Vector2, bool> EnemyKilled;

        /// <summary>
        /// 플레이어가 피격 데미지를 받은 직후 발행. damage 는 실제 감산된 값.
        /// </summary>
        event Action<int> PlayerTakeDamage;

        /// <summary>플레이어 사망 시 발행.</summary>
        event Action PlayerDeath;

        /// <summary>
        /// 레벨업 선택지 생성 직전 발행. Gambler / Greed / Sacrifice 가 선택지를 조작하기 위해 구독.
        /// </summary>
        event Action LevelUpChoice;
    }
}
