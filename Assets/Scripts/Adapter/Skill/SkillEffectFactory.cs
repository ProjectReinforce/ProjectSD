using System;
using System.Collections.Generic;
using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// SkillEffect 동적 생성 팩토리.
    /// SkillManager의 switch문을 대체하여 OCP(개방-폐쇄 원칙)를 준수.
    ///
    /// 사용법:
    /// - 초기화 시 Register()로 이펙트 타입 등록
    /// - SkillManager.CreateSkillSlot()에서 Create()로 이펙트 생성
    ///
    /// 확장 방법 (Phase 5):
    /// 새 SkillEffect 타입 추가 시:
    /// 1) AreaEffect.cs 등 SkillEffect 서브클래스 작성
    /// 2) SkillEffectFactory.RegisterDefaults()에 Register() 한 줄 추가
    /// 3) SkillData SO에서 effectType 선택
    /// → SkillManager 코드 수정 불필요!
    ///
    /// 설계 결정:
    /// - Reflection 대신 Dictionary + Func 사용 → 타입 안전 + 성능
    /// - static이 아닌 인스턴스 기반 → 테스트 시 Mock 가능
    /// - 생성 시 SkillData를 함께 전달 → 이펙트별 초기화 처리
    /// </summary>
    public class SkillEffectFactory
    {
        /// <summary>
        /// 이펙트 생성 함수 시그니처.
        /// GameObject: 이펙트가 부착될 스킬 슬롯 오브젝트.
        /// SkillData: 이펙트 초기화에 필요한 데이터.
        /// 반환: 생성된 SkillEffect 컴포넌트.
        /// </summary>
        public delegate SkillEffect EffectCreator(GameObject slotObj, SkillData data);

        private readonly Dictionary<SkillEffectType, EffectCreator> creators
            = new Dictionary<SkillEffectType, EffectCreator>();

        // ===== 등록 =====

        /// <summary>
        /// 이펙트 타입과 생성 함수 등록.
        /// 이미 등록된 타입을 다시 등록하면 덮어쓰기.
        /// </summary>
        public void Register(SkillEffectType type, EffectCreator creator)
        {
            if (creator == null)
            {
                Debug.LogError($"[SkillEffectFactory] null creator 등록 시도: {type}");
                return;
            }

            creators[type] = creator;
            Debug.Log($"[SkillEffectFactory] 등록: {type}");
        }

        /// <summary>
        /// 등록 해제. 테스트 또는 런타임 교체용.
        /// </summary>
        public bool Unregister(SkillEffectType type)
        {
            return creators.Remove(type);
        }

        /// <summary>
        /// 해당 타입이 등록되어 있는지 확인.
        /// </summary>
        public bool IsRegistered(SkillEffectType type)
        {
            return creators.ContainsKey(type);
        }

        // ===== 생성 =====

        /// <summary>
        /// SkillEffectType에 맞는 SkillEffect 컴포넌트를 slotObj에 생성.
        /// 미등록 타입이면 null 반환 + 경고 로그.
        /// </summary>
        /// <param name="type">SkillData.effectType</param>
        /// <param name="slotObj">이펙트가 부착될 GameObject</param>
        /// <param name="data">스킬 데이터 (이펙트 초기화용)</param>
        /// <returns>생성된 SkillEffect 또는 null</returns>
        public SkillEffect Create(SkillEffectType type, GameObject slotObj, SkillData data)
        {
            if (type == SkillEffectType.None)
                return null;

            if (!creators.TryGetValue(type, out EffectCreator creator))
            {
                Debug.LogWarning($"[SkillEffectFactory] 미등록 SkillEffectType: {type}. " +
                                 "Register()로 먼저 등록하세요.");
                return null;
            }

            try
            {
                SkillEffect effect = creator(slotObj, data);
                if (effect == null)
                    Debug.LogWarning($"[SkillEffectFactory] {type} 생성 결과가 null");
                return effect;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkillEffectFactory] {type} 생성 중 오류: {ex.Message}");
                return null;
            }
        }

        // ===== 기본 등록 =====

        /// <summary>
        /// 기본 이펙트 타입들을 등록.
        /// SkillManager.Awake() 또는 초기화 시점에서 호출.
        ///
        /// Phase 5 확장 시 이 메서드에 Register() 추가:
        /// - AreaEffect
        /// - OrbitalEffect
        /// - PlacedEffect
        /// - DebuffEffect
        /// </summary>
        public void RegisterDefaults()
        {
            // ── Phase 2~4: Projectile ──
            Register(SkillEffectType.Projectile, (slotObj, data) =>
            {
                var effect = slotObj.AddComponent<ProjectileEffect>();
                if (data.projectilePrefab != null)
                    effect.SetProjectilePrefab(data.projectilePrefab);
                else
                    Debug.LogWarning($"[SkillEffectFactory] {data.skillName}: projectilePrefab 미설정!");
                return effect;
            });

            // ── Phase 5 확장 지점 ──
            // 아래 주석을 해제하고 해당 클래스를 구현하면 바로 사용 가능

            // Register(SkillEffectType.Area, (slotObj, data) =>
            // {
            //     var effect = slotObj.AddComponent<AreaEffect>();
            //     effect.Initialize(data);
            //     return effect;
            // });

            // Register(SkillEffectType.Orbital, (slotObj, data) =>
            // {
            //     var effect = slotObj.AddComponent<OrbitalEffect>();
            //     effect.Initialize(data);
            //     return effect;
            // });

            // Register(SkillEffectType.Placed, (slotObj, data) =>
            // {
            //     var effect = slotObj.AddComponent<PlacedEffect>();
            //     effect.Initialize(data);
            //     return effect;
            // });

            // Register(SkillEffectType.Debuff, (slotObj, data) =>
            // {
            //     var effect = slotObj.AddComponent<DebuffEffect>();
            //     effect.Initialize(data);
            //     return effect;
            // });

            Debug.Log($"[SkillEffectFactory] 기본 등록 완료. 등록된 타입 수: {creators.Count}");
        }
    }
}