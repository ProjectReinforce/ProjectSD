using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "CharacterData", menuName = "ProjectSD/Data/Character Data")]
    public class CharacterData : ScriptableObject
    {
        public int id;
        public string displayName;
        public float baseMoveSpeed = 5f;
        public float baseHealth = 100f;
        public Sprite portrait;
    }
}
