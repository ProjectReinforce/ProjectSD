using UnityEngine;
using SwDreams.Data;

namespace SwDreams.Testing
{
    /// <summary>
    /// 테스트용 자동 색상 지정.
    /// SpriteRenderer가 있는 오브젝트에 부착하면
    /// 태그/컴포넌트 기반으로 색상을 자동 지정.
    /// 
    /// Phase 3: 적 타입별 색상 구분.
    /// Phase 7에서 실제 아트 적용 시 제거.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlaceholderColor : MonoBehaviour
    {
        private static readonly Color PlayerColor = new Color(0.2f, 0.6f, 1f);        // 파랑
        private static readonly Color ProjectileColor = new Color(1f, 1f, 0.3f);      // 노랑
        private static readonly Color OrbColor = new Color(0.3f, 1f, 0.4f);           // 초록

        // 적 타입별 색상
        private static readonly Color ChaserColor = new Color(1f, 0.3f, 0.3f);        // 빨강
        private static readonly Color RunnerColor = new Color(1f, 0.6f, 0.1f);        // 주황
        private static readonly Color TankColor = new Color(0.6f, 0.2f, 0.8f);        // 보라
        private static readonly Color SwarmColor = new Color(1f, 0.85f, 0.2f);        // 연노랑

        private SpriteRenderer sr;
        private Adapter.Entity.Enemy enemyRef;

        private void Start()
        {
            sr = GetComponent<SpriteRenderer>();

            if (sr.sprite == null)
                sr.sprite = CreateDefaultSprite();

            sr.color = GetColorByType();
        }

        /// <summary>
        /// Enemy.Initialize 이후 호출되도록 LateUpdate에서 한 번 갱신.
        /// Enemy는 풀에서 꺼낸 뒤 Initialize로 타입이 바뀔 수 있으므로,
        /// 활성화 직후 색상을 다시 적용.
        /// </summary>
        private void OnEnable()
        {
            // 다음 프레임에 색상 갱신 (Initialize가 같은 프레임에 호출되므로)
            Invoke(nameof(RefreshColor), 0f);
        }

        private void RefreshColor()
        {
            if (sr == null) return;
            sr.color = GetColorByType();
        }

        private Color GetColorByType()
        {
            // 적 타입별 색상
            enemyRef = GetComponent<Adapter.Entity.Enemy>();
            if (enemyRef != null)
            {
                switch (enemyRef.EnemyType)
                {
                    case EnemyType.Chaser: return ChaserColor;
                    case EnemyType.Runner: return RunnerColor;
                    case EnemyType.Tank:   return TankColor;
                    case EnemyType.Swarm:  return SwarmColor;
                }
                return ChaserColor;
            }

            if (GetComponent<Adapter.Skill.Projectile>())      return ProjectileColor;
            if (GetComponent<Adapter.Entity.ExperienceOrb>())   return OrbColor;
            if (CompareTag("Player"))                           return PlayerColor;

            return Color.white;
        }

        private Sprite CreateDefaultSprite()
        {
            Texture2D tex = new Texture2D(16, 16);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        }
    }
}