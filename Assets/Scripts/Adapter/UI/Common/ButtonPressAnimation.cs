using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace Adapter.UI.Common
{
    /// <summary>
    /// 버튼 누르기 애니메이션.
    /// DOTween으로 프레스 시 스케일 축소, 릴리즈 시 복귀.
    ///
    /// 범용 컴포넌트: 어떤 버튼에든 부착만 하면 동작.
    /// ButtonHoverEffect와 독립적으로 사용 가능.
    ///
    /// 셋업: 버튼 오브젝트에 부착. Inspector에서 수치 조절.
    /// </summary>
    public class ButtonPressAnimation : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("스케일 설정")]
        [SerializeField] private float pressScale = 0.9f;
        [SerializeField] private float pressDuration = 0.1f;
        [SerializeField] private float releaseDuration = 0.15f;
        [SerializeField] private Ease pressEase = Ease.InQuad;
        [SerializeField] private Ease releaseEase = Ease.OutBack;

        private Vector3 originalScale;
        private Tween scaleTween;
        private bool isPressed;

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            transform.localScale = originalScale;
            isPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            AnimateScale(originalScale * pressScale, pressDuration, pressEase);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            AnimateScale(originalScale, releaseDuration, releaseEase);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 누른 채로 밖으로 나간 경우 복귀
            if (isPressed)
            {
                isPressed = false;
                AnimateScale(originalScale, releaseDuration, releaseEase);
            }
        }

        private void AnimateScale(Vector3 target, float duration, Ease ease)
        {
            scaleTween?.Kill();
            scaleTween = transform.DOScale(target, duration).SetEase(ease).SetUpdate(true);
        }
    }
}
