using System.Collections.Generic;
using UnityEngine;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Weapon.Adapter.Data
{
    /// <summary>
    /// 모든 WeaponData 를 보관하는 SO 루트. GameManager.WeaponDB 로 SSOT 노출.
    ///
    /// DropSpawner: 등급 기반 무작위 룰 시 <see cref="GetRandomByRarity"/>.
    /// PlayerWeaponInventory: 조합 결과 id → WeaponData 해결에 <see cref="GetById"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "SwDreams/Data/WeaponDatabase")]
    public class WeaponDatabase : ScriptableObject
    {
        [SerializeField] private List<WeaponData> weapons = new List<WeaponData>();

        private Dictionary<string, WeaponData> idLookup;

        public IReadOnlyList<WeaponData> All => weapons;

        public WeaponData GetById(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            EnsureLookup();
            return idLookup.TryGetValue(weaponId, out var data) ? data : null;
        }

        /// <summary>
        /// 등급에 속한 무기 중 무작위 하나 반환. rng 는 호출자가 주입(호스트 결정 경로 시드 공유용).
        /// 후보가 없으면 null — DropSpawner 가 가중치 fallback 로 처리.
        /// </summary>
        public WeaponData GetRandomByRarity(Rarity rarity, System.Random rng)
        {
            if (weapons == null || weapons.Count == 0) return null;

            int count = 0;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].rarity == rarity)
                    count++;
            }
            if (count == 0) return null;

            int pick = rng != null ? rng.Next(count) : UnityEngine.Random.Range(0, count);
            int seen = 0;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null || weapons[i].rarity != rarity) continue;
                if (seen == pick) return weapons[i];
                seen++;
            }
            return null;
        }

        private void EnsureLookup()
        {
            if (idLookup != null && idLookup.Count == weapons.Count) return;
            idLookup = new Dictionary<string, WeaponData>(weapons.Count);
            for (int i = 0; i < weapons.Count; i++)
            {
                var w = weapons[i];
                if (w == null || string.IsNullOrEmpty(w.weaponId)) continue;
                idLookup[w.weaponId] = w;
            }
        }

        private void OnValidate()
        {
            idLookup = null;
        }
    }
}
