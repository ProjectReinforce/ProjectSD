using UnityEngine;
using SwDreams.Features.Character.Adapter.Data;

namespace SwDreams.Features.Character.Adapter.Data
{
    /// <summary>
    /// 전체 캐릭터 데이터를 중앙 관리하는 ScriptableObject.
    /// SkillDatabase와 동일한 패턴.
    ///
    /// 셋업:
    ///   Assets/Data/ 폴더에서 Create → SwDreams/CharacterDatabase
    ///   인스펙터에서 모든 CharacterData SO를 연결.
    ///
    /// 사용:
    ///   GamePlayerSpawner에서 characterId → CharacterData 변환 시 사용.
    ///   ResultPanelUI에서 플레이어별 캐릭터 정보 표시 시 사용.
    ///
    /// GameManager.Instance에서 접근하거나,
    /// GamePlayerSpawner에서 직접 Inspector 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "SwDreams/CharacterDatabase")]
    public class CharacterDatabase : ScriptableObject
    {
        [Header("캐릭터 목록 (3종)")]
        public CharacterData[] characters;

        /// <summary>
        /// characterId로 CharacterData 검색.
        /// 대기실 CustomProperties에서 받은 ID로 SO 조회.
        /// </summary>
        public CharacterData GetById(int characterId)
        {
            if (characters == null) return null;

            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null && characters[i].id == characterId)
                    return characters[i];
            }

            Debug.LogWarning($"[CharacterDatabase] 캐릭터 ID {characterId} 찾기 실패");
            return null;
        }

        /// <summary>
        /// 기본 캐릭터 반환 (ID 매칭 실패 시 폴백).
        /// </summary>
        public CharacterData GetDefault()
        {
            if (characters != null && characters.Length > 0)
                return characters[0];

            Debug.LogError("[CharacterDatabase] 캐릭터가 하나도 없습니다!");
            return null;
        }

        // ===== 에디터 검증 =====
        private void OnValidate()
        {
            if (characters == null) return;

            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (var character in characters)
            {
                if (character == null) continue;
                if (!ids.Add(character.id))
                {
                    Debug.LogWarning($"[CharacterDatabase] 중복 캐릭터 ID: {character.id} ({character.displayName})");
                }
            }
        }
    }
}
