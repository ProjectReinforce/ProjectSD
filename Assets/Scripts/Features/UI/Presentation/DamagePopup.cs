using UnityEngine;
using SwDreams.Features.UI.Presentation;
using TMPro;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 떠오르는 데미지 숫자 표시.
    /// 풀링 기반. Enemy 피격 시 스폰.
    ///
    /// 프리팹 구성:
    /// - GameObject "DamagePopup"
    ///   - TextMeshPro (3D, not UI) 컴포넌트
    ///   - DamagePopup 스크립트
    ///   - SortingGroup 또는 SpriteRenderer 없이 MeshRenderer의 sortingOrder 조정
    ///
    /// 사용: DamagePopup.Spawn(position, damage)
    /// 프리팹은 GameplayConfig 또는 별도 SO에서 참조.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamagePopup : MonoBehaviour, IPoolable
    {
        [Header("연출 설정")]
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.6f;
        [SerializeField] private float fadeStartRatio = 0.4f; // lifetime의 40% 이후부터 페이드
        [SerializeField] private float scaleUpDuration = 0.1f;
        [SerializeField] private float initialScale = 0.5f;
        [SerializeField] private float maxScale = 1.2f;
        [SerializeField] private float finalScale = 0.8f;

        [Header("폰트")]
        [SerializeField] private float normalFontSize = 2f;
        [SerializeField] private float critFontSize = 2.8f;

        [Header("색상")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color critColor = new Color(1f, 0.85f, 0f, 1f); // 금색
        [SerializeField] private Color healColor = new Color(0.3f, 1f, 0.3f, 1f); // 초록

        // 프리팹 참조 (static — 한 번만 설정)
        private static GameObject popupPrefab;

        private TextMeshPro tmp;
        private float aliveTime;
        private bool isActive;
        private Vector3 randomOffset;

        private void Awake()
        {
            tmp = GetComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 100; // 최상위 표시

            // MeshRenderer sorting 설정 (스프라이트 위에 표시)
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = 100;
            }
        }

        /// <summary>
        /// 프리팹 등록. GameSceneInitializer 등에서 1회 호출.
        /// </summary>
        public static void SetPrefab(GameObject prefab)
        {
            popupPrefab = prefab;
            PoolManager.Instance?.Prewarm(prefab, 20);
        }

        /// <summary>
        /// 데미지 팝업 스폰. 모든 클라이언트에서 로컬 호출 가능.
        /// </summary>
        public static void Spawn(Vector3 position, int amount, bool isCrit = false, bool isHeal = false)
        {
            if (PoolManager.Instance == null) return;

            // 프리팹 자동 등록 (최초 1회)
            if (popupPrefab == null)
            {
                var cfg = GameManager.Instance?.Config;
                if (cfg != null && cfg.damagePopupPrefab != null)
                    SetPrefab(cfg.damagePopupPrefab);
            }

            if (popupPrefab == null) return;

            var obj = PoolManager.Instance.Get(popupPrefab);
            var popup = obj.GetComponent<DamagePopup>();
            if (popup != null)
                popup.Setup(position, amount, isCrit, isHeal);
        }

        private void Setup(Vector3 position, int amount, bool isCrit, bool isHeal)
        {
            // 약간의 랜덤 오프셋으로 겹침 방지
            randomOffset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(0f, 0.2f),
                0f
            );
            transform.position = position + randomOffset;
            transform.localScale = Vector3.one * initialScale;

            // 텍스트 설정
            if (isHeal)
            {
                tmp.text = $"+{amount}";
                tmp.color = healColor;
            }
            else
            {
                tmp.text = amount.ToString();
                tmp.color = isCrit ? critColor : normalColor;
            }

            // 크리티컬이면 더 큰 폰트
            tmp.fontSize = isCrit ? critFontSize : normalFontSize;

            aliveTime = 0f;
            isActive = true;
        }

        private void Update()
        {
            if (!isActive) return;

            aliveTime += Time.deltaTime;

            if (aliveTime >= lifetime)
            {
                isActive = false;
                PoolManager.Instance?.Return(gameObject);
                return;
            }

            float t = aliveTime / lifetime;

            // 위로 떠오르기
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            // 스케일 연출: 빠르게 커졌다가 안정
            float scale;
            if (aliveTime < scaleUpDuration)
            {
                float st = aliveTime / scaleUpDuration;
                scale = Mathf.Lerp(initialScale, maxScale, st);
            }
            else
            {
                float st = (aliveTime - scaleUpDuration) / (lifetime - scaleUpDuration);
                scale = Mathf.Lerp(maxScale, finalScale, st);
            }
            transform.localScale = Vector3.one * scale;

            // 페이드 아웃
            if (t > fadeStartRatio)
            {
                float fadeT = (t - fadeStartRatio) / (1f - fadeStartRatio);
                Color c = tmp.color;
                c.a = Mathf.Lerp(1f, 0f, fadeT);
                tmp.color = c;
            }
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            aliveTime = 0f;
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }
}