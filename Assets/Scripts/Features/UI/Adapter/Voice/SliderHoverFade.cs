using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 슬라이더의 핸들/트랙 Image 알파를 부모 VoicePanelHover 의 호버 상태에 따라 페이드 (R14).
    /// 평소: 핸들 alpha=0 (사라짐), 트랙 alpha=0.4 (옅음).
    /// 호버: 모두 alpha=1.
    /// 슬라이더 옆 또는 슬라이더 root 에 부착, Image 슬롯 인스펙터에서 채움.
    ///
    /// `panelHover` 슬롯 비워두면 GetComponentInParent 로 자동 탐색 — 못 찾으면 비활성.
    /// 명시 드래그하면 GetComponentInParent 함정 회피 (계층 구조/비활성 부모/prefab race).
    /// </summary>
    public class SliderHoverFade : MonoBehaviour
    {
        [SerializeField, Tooltip("비워두면 GetComponentInParent 로 자동 탐색. 안전상 명시 드래그 권장.")]
        private VoicePanelHover panelHover;

        [SerializeField] private Image handleImage;
        [SerializeField] private Image trackBgImage;
        [SerializeField] private Image trackFillImage;

        [Header("Idle (호버 X)")]
        [SerializeField, Range(0f, 1f)] private float idleHandleAlpha = 0f;
        [SerializeField, Range(0f, 1f)] private float idleTrackAlpha = 0.4f;

        [Header("Hover")]
        [SerializeField, Range(0f, 1f)] private float hoverHandleAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float hoverTrackAlpha = 1f;

        [SerializeField] private float fadeSpeed = 8f;

        private void Awake()
        {
            // 인스펙터 슬롯이 비어있으면 자동 탐색.
            if (panelHover == null)
                panelHover = GetComponentInParent<VoicePanelHover>(includeInactive: true);

            // 시작 알파 = idle.
            SetAlpha(handleImage, idleHandleAlpha);
            SetAlpha(trackBgImage, idleTrackAlpha);
            SetAlpha(trackFillImage, idleTrackAlpha);

            if (panelHover == null)
                Debug.LogWarning($"[SliderHoverFade] panelHover 미연결 ({name}). " +
                                 "VoicePanelHover 가 부모 계층에 없거나 명시 드래그 필요. 호버 페이드 비활성.", this);
        }

        private void OnEnable()
        {
            // panelHover null 이어도 비활성화 X — Awake 에서 박은 idle alpha 유지.
            // Update 가 panelHover null 가드로 noop.
        }

        private void Update()
        {
            if (panelHover == null) return; // 미연결 — Awake 의 idle alpha 유지
            float t = panelHover.IsHover ? 1f : 0f;
            float dt = fadeSpeed * Time.unscaledDeltaTime;

            if (handleImage != null)
                FadeTowards(handleImage, Mathf.Lerp(idleHandleAlpha, hoverHandleAlpha, t), dt);
            if (trackBgImage != null)
                FadeTowards(trackBgImage, Mathf.Lerp(idleTrackAlpha, hoverTrackAlpha, t), dt);
            if (trackFillImage != null)
                FadeTowards(trackFillImage, Mathf.Lerp(idleTrackAlpha, hoverTrackAlpha, t), dt);
        }

        private static void FadeTowards(Image img, float target, float dt)
        {
            var c = img.color;
            c.a = Mathf.MoveTowards(c.a, target, dt);
            img.color = c;
        }

        private static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color; c.a = a; img.color = c;
        }
    }
}
