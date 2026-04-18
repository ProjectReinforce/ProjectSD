using System;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Adapter;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// SkillEffectType → ISkillSpawner 생성 팩토리.
    /// 기존 SkillEffectFactory를 대체.
    ///
    /// [Step 4-6] SkillEffect 레이어 제거.
    /// - SkillEffect 대신 ISkillSpawner를 직접 반환
    /// - Skill.Fire()가 Executor를 직접 생성하므로 Effect 중간 레이어 불필요
    ///
    /// 확장 방법:
    /// 1) 새 ISkillSpawner 구현 (PathSpawner 등)
    /// 2) RegisterDefaults()에 Register() 한 줄 추가
    /// → SkillManager/Skill 코드 수정 불필요
    /// </summary>
    public class SkillSpawnerFactory
    {
        public delegate ISkillSpawner SpawnerCreator(SkillData data);

        private readonly Dictionary<SkillEffectType, SpawnerCreator> creators
            = new Dictionary<SkillEffectType, SpawnerCreator>();

        // ===== 등록 =====

        public void Register(SkillEffectType type, SpawnerCreator creator)
        {
            if (creator == null)
            {
                Debug.LogError($"[SkillSpawnerFactory] null creator 등록 시도: {type}");
                return;
            }
            creators[type] = creator;
        }

        public bool Unregister(SkillEffectType type)
        {
            return creators.Remove(type);
        }

        public bool IsRegistered(SkillEffectType type)
        {
            return creators.ContainsKey(type);
        }

        // ===== 생성 =====

        /// <summary>
        /// SkillEffectType에 맞는 ISkillSpawner 생성.
        /// 패시브/혼돈 등 스폰이 없는 타입은 null 반환.
        /// </summary>
        public ISkillSpawner Create(SkillEffectType type, SkillData data)
        {
            if (type == SkillEffectType.None)
                return null;

            if (!creators.TryGetValue(type, out SpawnerCreator creator))
            {
                Debug.LogWarning($"[SkillSpawnerFactory] 미등록 SkillEffectType: {type}");
                return null;
            }

            try
            {
                ISkillSpawner spawner = creator(data);
                if (spawner == null)
                    Debug.LogWarning($"[SkillSpawnerFactory] {type} 생성 결과가 null");
                return spawner;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillSpawnerFactory] {type} 생성 중 오류: {ex.Message}");
                return null;
            }
        }

        // ===== 기본 등록 =====

        /// <summary>
        /// 기본 Spawner 타입들을 등록.
        /// SkillManager.Awake()에서 호출.
        /// </summary>
        public void RegisterDefaults()
        {
            // ── Projectile (투사체형) ──
            Register(SkillEffectType.Projectile, (data) =>
            {
                if (data.projectilePrefab == null)
                    Debug.LogWarning($"[SkillSpawnerFactory] {data.skillName}: projectilePrefab 미설정!");
                var spawner = new ProjectileSpawner(data.projectilePrefab);
                spawner.Prewarm(data);
                return spawner;
            });

            // ── Area (장판형) ──
            Register(SkillEffectType.Area, (data) =>
            {
                if (data.effectPrefab == null)
                    Debug.LogWarning($"[SkillSpawnerFactory] {data.skillName}: effectPrefab 미설정!");
                var spawner = new AreaSpawner(data.effectPrefab, data.maxInstances);
                spawner.Prewarm(data);
                return spawner;
            });

            // ── Orbital (회전형) ──
            Register(SkillEffectType.Orbital, (data) =>
            {
                if (data.effectPrefab == null)
                    Debug.LogWarning($"[SkillSpawnerFactory] {data.skillName}: effectPrefab 미설정!");
                var spawner = new OrbitalSpawner(data.effectPrefab);
                spawner.Prewarm(data);
                return spawner;
            });

            // ── Placed (설치형) ──
            Register(SkillEffectType.Placed, (data) =>
            {
                if (data.effectPrefab == null)
                    Debug.LogWarning($"[SkillSpawnerFactory] {data.skillName}: effectPrefab 미설정!");
                var spawner = new PlacedSpawner(data.effectPrefab, data.maxInstances);
                spawner.Prewarm(data);
                return spawner;
            });

            // ── Debuff (디버프형) ──
            Register(SkillEffectType.Debuff, (data) =>
            {
                var spawner = new DebuffSpawner(data.effectPrefab, data.spreadOnDeathCount);
                spawner.Prewarm(data);
                return spawner;
            });

            Debug.Log($"[SkillSpawnerFactory] 기본 등록 완료. 등록된 타입 수 : {creators.Count}");
        }
    }
}