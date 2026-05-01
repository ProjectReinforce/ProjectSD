using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SwDreams.Features.UI.Adapter.Voice
{
    /// <summary>
    /// 인게임 좌측 보이스 패널의 호버 상태 감지 + root CanvasGroup alpha 페이드 (R14).
    /// 평소 idleAlpha (흐림) ↔ 호버 hoverAlpha (또렷). 자식 SliderHoverFade 가 자기 핸들/트랙 알파를
    /// 별도 처리하기 위해 OnHoverChanged 이벤트 발행.
    ///
    /// 인터랙션은 가드 X — Slider 컴포넌트 default 동작 그대로. 시각만 차이.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class VoicePanelHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.5f;
        [SerializeField, Range(0f, 1f)] private float hoverAlpha = 1.0f;
        [SerializeField, Tooltip("alpha 변화 속도 (1초당). 8 = 0.125초에 0→1")]
        private float fadeSpeed = 8f;

        public bool IsHover { get; private set; }
        public event Action<bool> OnHoverChanged;

        private void Awake()
        {
            if (panelGroup == null) panelGroup = GetComponent<CanvasGroup>();
            if (panelGroup != null) panelGroup.alpha = idleAlpha;
        }

        private void Update()
        {
            if (panelGroup == null) return;
            float target = IsHover ? hoverAlpha : idleAlpha;
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, target,
                fadeSpeed * Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (IsHover) return;
            IsHover = true;
            OnHoverChanged?.Invoke(true);
        }

        public void OnPointerExit(PointerEventData _)
        {
            if (!IsHover) return;
            IsHover = false;
            OnHoverChanged?.Invoke(false);
        }
    }
}
