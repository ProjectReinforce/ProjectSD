using UnityEngine;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Domain.ValueObjects;
using UnityEditor;
using SwDreams.Shared.Data;

namespace SwDreams.Editor
{
    /// <summary>
    /// SkillData 및 모든 서브클래스의 인스펙터를 정리.
    /// skillType과 effectType에 따라 관련 필드만 표시.
    ///
    /// [Phase 7 리팩토링] Step 3-6
    /// </summary>
    [CustomEditor(typeof(SkillData), true)]
    public class SkillDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var skillType = (SkillType)serializedObject.FindProperty("skillType").enumValueIndex;
            var effectType = (SkillEffectType)serializedObject.FindProperty("effectType").enumValueIndex;

            // ===== 기본 정보 (항상 표시) =====
            EditorGUILayout.LabelField("기본 정보", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("skillId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("skillName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("skillType"));

            // effectType: 액티브만
            if (skillType == SkillType.Active)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("effectType"));

            // chaosEffectType: 혼돈만
            if (skillType == SkillType.Chaos)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("chaosEffectType"));

                // [Phase 8-A] ChaosSkillData 전용: 등급별 파라미터 (primary/secondary/tertiary).
                // 하나의 SO 가 4 등급 값을 모두 보유 → 런타임 rolledRarity 로 인덱싱.
                // ChaosSkillData 서브클래스의 필드이므로 해당 SO 일 때만 property 존재.
                var paramsProp = serializedObject.FindProperty("paramsByRarity");
                if (paramsProp != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("혼돈 파라미터 (등급별)", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "길이 4 (Common / Rare / Epic / Legendary).\n" +
                        "(primary, secondary, tertiary) 의미는 chaosEffectType 별로 다름.\n" +
                        "전부 0 이면 ChaosSkillManager 의 fallback 기본값을 사용.\n\n" +
                        "매핑:\n" +
                        "  GlassCannon    : primary=ATK 배율, secondary=HP 비율, tertiary=미사용\n" +
                        "  ChainExplosion : primary=폭발 데미지, secondary=반경, tertiary=미사용\n" +
                        "  BerserkMode    : primary=CDR 배율, secondary=HP 임계, tertiary=이속 배율\n" +
                        "  AccelEngine    : primary=최대 증폭, secondary=램프 시간, tertiary=미사용\n" +
                        "  Unity          : primary=1명 근접 시 보너스, secondary=추가 인당 증가, tertiary=감지 반경(0=기본값)\n" +
                        "  Gambler        : 파라미터 미사용",
                        MessageType.Info);
                    EditorGUILayout.PropertyField(paramsProp, true);
                }
            }

            // ===== UI 표시용 (항상) =====
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI 표시용", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

            // ===== 레벨 스케일링 (항상) =====
            // ===== 레벨 스케일링 (혼돈 제외 — Lv1 고정) =====
            if (skillType != SkillType.Chaos)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("레벨 스케일링", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxLevel"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("damagePerLevel"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldownPerLevel"), true);
            }

            // ===== 패시브 전용 =====
            if (skillType == SkillType.Passive)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("패시브 전용", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bonusType"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bonusPerLevel"));
            }

            // ===== 액티브 스킬 타입별 필드 =====
            if (skillType == SkillType.Active)
            {
                // 발사 모드 (Executor)
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("발사 모드 (Executor)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("firingMode"));

                var firingMode = (SwDreams.Features.Skill.Domain.ValueObjects.FiringMode)
                    serializedObject.FindProperty("firingMode").enumValueIndex;
                if (firingMode == SwDreams.Features.Skill.Domain.ValueObjects.FiringMode.DelayedBurst)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("burstDelay"));

                // 투사체 전용
                if (effectType == SkillEffectType.Projectile)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("투사체 전용", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("projectilePrefab"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileSpeed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileLifetime"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("penetrates"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("subProjectilePrefab"));

                    // 배치 패턴
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("배치/궤적", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("aimType"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadPattern"));

                    var spreadType = (SwDreams.Features.Skill.Domain.ValueObjects.SpreadPatternType)
                        serializedObject.FindProperty("spreadPattern").enumValueIndex;
                    if (spreadType == SwDreams.Features.Skill.Domain.ValueObjects.SpreadPatternType.Fan)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadAngle"));

                    // 궤적 패턴
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("trajectoryType"));

                    var trajType = (SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType)
                        serializedObject.FindProperty("trajectoryType").enumValueIndex;

                    // 궤적별 파라미터
                    if (trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.Homing)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("homingRotateSpeed"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("chainFlightCount"));
                        if (serializedObject.FindProperty("chainFlightCount").intValue > 0)
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("chainSearchRadius"));
                    }
                    else if (trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.Boomerang)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("hasPullOnReturn"));
                        if (serializedObject.FindProperty("hasPullOnReturn").boolValue)
                        {
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("pullRadius"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("pullForce"));
                        }
                    }
                    else if (trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.Tornado)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pullRadius"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pullForce"));
                    }
                    else if (trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.Spiral)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pullRadius"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pullForce"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("spiralExpandSpeed"));
                    }
                    else if (trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.Zigzag ||
                             trajType == SwDreams.Features.Skill.Domain.ValueObjects.TrajectoryType.SinWave)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("waveAmplitude"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("waveFrequency"));
                    }
                }

                // 장판 전용
                if (effectType == SkillEffectType.Area)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("범위/장판 전용", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaRadius"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaDuration"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("tickRate"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("isHealingEffect"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnAtRandomPosition"));

                    if (serializedObject.FindProperty("spawnAtRandomPosition").boolValue)
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSpawnRadius"));
                }

                // 회전형 전용
                if (effectType == SkillEffectType.Orbital)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("회전형 전용", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("orbitRadius"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("objectCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaDuration"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("knockbackForce"));

                    // TwoPhase: Phase2 투사체 설정 (장검 진화 등)
                    if (firingMode == SwDreams.Features.Skill.Domain.ValueObjects.FiringMode.TwoPhase)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("TwoPhase — Phase1 회전 + Phase2 투사체", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("phase1RotationCount"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectilePrefab"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileSpeed"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileCount"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileLifetime"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("penetrates"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimType"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadPattern"));

                        var spreadType = (SwDreams.Features.Skill.Domain.ValueObjects.SpreadPatternType)
                            serializedObject.FindProperty("spreadPattern").enumValueIndex;
                        if (spreadType == SwDreams.Features.Skill.Domain.ValueObjects.SpreadPatternType.Fan)
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadAngle"));

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("trajectoryType"));
                    }
                }

                // 설치형 전용
                if (effectType == SkillEffectType.Placed)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("설치형 전용", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("areaDuration"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("attackRange"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("attackCooldown"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("alwaysCritical"));
                }

                // 디버프 전용
                if (effectType == SkillEffectType.Debuff)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("디버프 전용", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("debuffDuration"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("damageAmplify"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCount"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("spreadOnDeathCount"));
                }

                // 공통 효과 (장판/설치/회전/디버프만 — 투사체는 미사용)
                if (effectType != SkillEffectType.Projectile)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("공통 효과", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("effectPrefab"));
                }

                // 패시브 적용 필터 (액티브만)
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("패시브 적용 필터", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "비어있으면 모든 패시브가 이 스킬에 적용됩니다.\n" +
                    "특정 스탯만 선택하면 해당 스탯 보너스만 적용됩니다.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("statOverrides"), true);

                // Trigger+Effect 조합 (액티브만)
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Trigger+Effect 조합", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "기본 추가 효과를 정의합니다. 진화 스킬에서 주로 사용.\n" +
                    "런타임 추가 효과(정수/무기/혼돈)는 SkillTriggerSystem에서 관리.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("triggerEffects"), true);
            }

            // ===== 진화 연결 (액티브만 — 역방향 체크가 자동 처리) =====
            if (skillType == SkillType.Active)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("진화 연결", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("evolutionPair"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("evolvedSkill"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}