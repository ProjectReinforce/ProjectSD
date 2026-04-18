using UnityEngine;
using SwDreams.Features.Skill.Adapter;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 발사 기록 인터페이스. 메아리(#17) 스킬에서 사용.
    ///
    /// Executor가 FireOnce()에서 기록하고, 메아리 스킬이 읽어서 재현.
    /// 진화형(직전 2개 재현)도 기록 2개 읽으면 대응.
    ///
    /// 현재는 stub — 메아리 구현 시 구현체 작성.
    ///
    /// [Phase 7 리팩토링] Step 4-7
    /// </summary>
    public interface IFireRecorder
    {
        /// <summary>
        /// 발사 기록 추가.
        /// Executor.FireOnce()에서 매 발사 시 호출.
        /// </summary>
        void Record(FireRecord record);

        /// <summary>
        /// 가장 최근 기록 반환. 없으면 null.
        /// 메아리 기본형: 직전 1개 재현.
        /// </summary>
        FireRecord GetLatest();

        /// <summary>
        /// 최근 n개 기록 반환.
        /// 메아리 진화형: 직전 2개 재현.
        /// </summary>
        FireRecord[] GetLatest(int count);

        /// <summary>
        /// 기록 초기화. 게임 시작/재시작 시.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 단일 발사 기록.
    /// 메아리 스킬이 데미지 계수만 낮춰서 재실행할 때 사용.
    /// </summary>
    public struct FireRecord
    {
        /// <summary>발사한 스킬 ID.</summary>
        public int skillId;

        /// <summary>발사 위치.</summary>
        public Vector2 position;

        /// <summary>발사 방향.</summary>
        public Vector2 direction;

        /// <summary>발사 시각 (Time.time).</summary>
        public float timestamp;

        /// <summary>사용된 Spawner의 SkillEffectType.</summary>
        public Data.SkillEffectType effectType;
    }
}
