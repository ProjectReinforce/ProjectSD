using UnityEngine;

namespace SwDreams.Testing
{
    /// <summary>
    /// 테스트용 자동 색상 지정.
    /// SpriteRenderer가 있는 오브젝트에 부착하면
    /// 태그/컴포넌트 기반으로 색상을 자동 지정.
    /// 
    /// 프리팹에 붙여두면 스폰 시 자동으로 색이 바뀜.
    /// Phase 7에서 실제 아트 적용 시 제거.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlaceholderColor : MonoBehaviour
    {
        private static readonly Color PlayerColor = new Color(0.2f, 0.6f, 1f);     // 파랑
        private static readonly Color EnemyColor = new Color(1f, 0.3f, 0.3f);      // 빨강
        private static readonly Color ProjectileColor = new Color(1f, 1f, 0.3f);   // 노랑
        private static readonly Color OrbColor = new Color(0.3f, 1f, 0.4f);        // 초록

        private void Start()
        {
            var sr = GetComponent<SpriteRenderer>();

            // 기본 스프라이트 없으면 Unity 내장 흰색 사각형 사용
            if (sr.sprite == null)
                sr.sprite = CreateDefaultSprite();

            sr.color = GetColorByType();
        }

        private Color GetColorByType()
        {
            if (GetComponent<Adapter.Entity.Enemy>())          return EnemyColor;
            if (GetComponent<Adapter.Skill.Projectile>())      return ProjectileColor;
            if (GetComponent<Adapter.Entity.ExperienceOrb>())  return OrbColor;
            if (CompareTag("Player"))                          return PlayerColor;

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