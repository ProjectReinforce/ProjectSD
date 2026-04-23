using UnityEngine;
using SwDreams.Features.Essence.Domain;

namespace SwDreams.Features.Essence.Adapter.Data
{
    /// <summary>
    /// 속성별 EssenceData 를 조회하는 SO 루트.
    /// PlayerEssenceInventory 가 Inspector 로 연결받아 lookup 용도로 사용.
    ///
    /// 3종이 고정이므로 개별 필드로 노출 — 오타/null 을 Inspector 에서 바로 잡을 수 있게.
    /// </summary>
    [CreateAssetMenu(fileName = "EssenceDatabase", menuName = "SwDreams/Data/EssenceDatabase")]
    public class EssenceDatabase : ScriptableObject
    {
        [SerializeField] private EssenceData ice;
        [SerializeField] private EssenceData fire;
        [SerializeField] private EssenceData lightning;

        public EssenceData GetByType(EssenceType type)
        {
            switch (type)
            {
                case EssenceType.Ice:       return ice;
                case EssenceType.Fire:      return fire;
                case EssenceType.Lightning: return lightning;
                default:                    return null;
            }
        }
    }
}
