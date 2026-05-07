using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Character.Adapter;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Enemy.Adapter.Attack
{
    /// <summary>
    /// 경고 비주얼 → 지연 폭발 존.
    /// 원거리 적의 Telegraph 공격, 추후 엘리트 출현 경고/스킬 예고에도 재사용 가능.
    ///
    /// DOTween 등 외부 트윈 금지: GameState.Paused 와 동기화되기 위해
    /// Time.deltaTime 기반 자체 타이머 + SpriteRenderer.color / transform.localScale 수동 lerp.
    /// Playing/BossFight 상태가 아니면 타이머 정지.
    ///
    /// Strike(데미지) 판정은 호스트만 수행.
    /// </summary>
    public class TelegraphZone : MonoBehaviour, IPoolable
    {
        [Header("채움 모델 비주얼")]
        [Tooltip("외곽 링(흐린 빨강, 정적). 프리팹 자식의 SpriteRenderer 를 드래그. null 허용.")]
        [SerializeField] private SpriteRenderer outerSprite;
        [Tooltip("중심 채움(진한 빨강). 이 Transform 의 localScale 을 0→1 로 lerp 해서 진행도 표시.\n" +
                 "프리팹에서 이 자식의 localScale=1 이 Outer 와 동일 크기가 되도록 맞추기.")]
        [SerializeField] private Transform innerFill;

        [Header("스케일 처리")]
        [Tooltip("true: Initialize 시 radius 에 맞춰 localScale 을 자동 조정. 프리팹의 수동 스케일은 덮어씀.\n" +
                 "false: 프리팹에서 맞춘 스케일 그대로 유지. Strike 반경은 SO radius 로 독립 동작.")]
        [SerializeField] private bool autoScaleToRadius = false;

        [Tooltip("autoScaleToRadius=true 일 때 사용. 프리팹 localScale=1 상태에서의 월드 반경.\n" +
                 "Unity 기본 원형 스프라이트(1×1)는 0.5. 런타임 scale = radius / prefabUnitRadius.")]
        [SerializeField] private float prefabUnitRadius = 0.5f;

        private float duration;
        private float radius;
        private int damage;
        private float elapsed;
        private bool isActive;

        // B-1a: 발사 적의 EnemyId — Player 사망 시 LastDamagerEnemyId 진입점.
        private int sourceEnemyId;

        // 기본 원형 스프라이트 (한 번만 생성해서 모든 인스턴스가 공유)
        private static Sprite cachedCircleSprite;

        private void Awake()
        {
            EnsureDefaultCircleSprites();
        }

        /// <summary>
        /// Outer/Inner/Legacy SpriteRenderer 중 sprite 가 비어 있으면 기본 원형으로 채움.
        /// 사용자가 프리팹에 이미지를 지정하지 않아도 동작하도록 보장.
        /// </summary>
        private void EnsureDefaultCircleSprites()
        {
            if (outerSprite != null && outerSprite.sprite == null)
                outerSprite.sprite = GetDefaultCircleSprite();

            if (innerFill != null)
            {
                var innerSr = innerFill.GetComponent<SpriteRenderer>();
                if (innerSr == null)
                    innerSr = innerFill.GetComponentInChildren<SpriteRenderer>();
                if (innerSr != null && innerSr.sprite == null)
                    innerSr.sprite = GetDefaultCircleSprite();
            }
        }

        /// <summary>
        /// 런타임에 1회 생성하는 흰색 원형 Sprite.
        /// PPU = size 로 설정해서 localScale=1 일 때 월드 1×1 (반경 0.5) 크기가 되도록 함.
        /// 색상/alpha 는 SpriteRenderer.color 에서 제어.
        /// </summary>
        private static Sprite GetDefaultCircleSprite()
        {
            if (cachedCircleSprite != null) return cachedCircleSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // 1픽셀 폭 안티에일리어싱 엣지
                    float a = Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            cachedCircleSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size // PPU = size → 1 world unit 직경
            );
            cachedCircleSprite.name = "TelegraphZoneDefaultCircle";
            cachedCircleSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedCircleSprite;
        }

        public void Initialize(Vector2 pos, float duration, float radius, int damage, int sourceEnemyId = 0)
        {
            transform.position = pos;
            this.duration = Mathf.Max(0.01f, duration);
            this.radius = radius;
            this.damage = damage;
            this.sourceEnemyId = sourceEnemyId;
            this.elapsed = 0f;
            this.isActive = true;

            // 기본값은 '프리팹 수동 스케일 존중'. SO radius 는 Strike 판정에만 쓰임.
            // 자동 맞춤을 원하면 Inspector 에서 autoScaleToRadius=true + prefabUnitRadius 정확히.
            if (autoScaleToRadius)
            {
                float baseR = Mathf.Max(0.0001f, prefabUnitRadius);
                float mult = radius / baseR;
                transform.localScale = new Vector3(mult, mult, 1f);
            }

            // Inner 채움은 중심에서 시작 — outer/inner 색상 및 alpha 는 prefab 인스펙터에서 관리.
            if (innerFill != null)
                innerFill.localScale = Vector3.zero;

            ApplyVisual(0f);
        }

        private void Update()
        {
            if (!isActive) return;

            // 씬 전환 중(GameManager 파괴)엔 안전하게 정지
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight)
                return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyVisual(t);

            if (elapsed >= duration)
            {
                Strike();
                ReturnToPool();
            }
        }

        private void ApplyVisual(float t)
        {
            // 중심 채움 — Inner 가 0 → 1 로 scale lerp.
            if (innerFill != null)
            {
                float s = Mathf.Clamp01(t);
                innerFill.localScale = new Vector3(s, s, 1f);
            }
        }

        private void Strike()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            Vector2 center = transform.position;
            float sqrRadius = radius * radius;

            foreach (var p in players)
            {
                if (p == null || !p.activeInHierarchy) continue;

                float sqrDist = ((Vector2)p.transform.position - center).sqrMagnitude;
                if (sqrDist > sqrRadius) continue;

                var player = p.GetComponent<PlayerStub>();
                if (player != null && player.IsAlive)
                    player.TakeDamageFromEnemy(damage, sourceEnemyId);
            }
        }

        private void ReturnToPool()
        {
            isActive = false;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
        }

        public void OnReturnToPool()
        {
            isActive = false;

            // 다음 스폰 직전 잔상 방지 — Inner scale 만 0 으로.
            if (innerFill != null)
                innerFill.localScale = Vector3.zero;

            gameObject.SetActive(false);
        }
    }
}
