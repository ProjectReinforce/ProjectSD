using UnityEngine;
using SwDreams.Features.UI.Adapter.Common;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SwDreams.Features.UI.Adapter.Common
{
    /// <summary>
    /// 버튼 호버/프레스 비주얼 효과.
    /// 텍스트 색상 변경 + 이미지 스프라이트 교체.
    ///
    /// 배열 지원: 버튼에 텍스트나 이미지가 여러 개인 경우 대응.
    ///   - texts 배열의 각 요소에 동일한 색상 변경 적용.
    ///   - images 배열의 각 요소에 개별 normalSprite/hoverSprite 적용.
    ///
    /// 범용 컴포넌트: ButtonPressAnimation과 독립적으로 사용 가능.
    ///
    /// 셋업:
    ///   버튼 오브젝트에 부착.
    ///   texts: 색상이 바뀔 TMP_Text들을 등록.
    ///   images: 스프라이트가 바뀔 Image들을 등록 (ImageSwapEntry 배열).
    ///   Button 컴포넌트의 Transition은 None으로 설정 권장.
    /// </summary>
    public class ButtonHoverEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        // ===== 텍스트 색상 =====

        [Header("텍스트 색상")]
        [Tooltip("색상이 바뀔 TMP_Text 목록 (비워두면 자식에서 자동 검색)")]
        [SerializeField] private TMP_Text[] texts;
        [SerializeField] private Color normalTextColor = new Color(0.427f, 0.255f, 0.490f, 1f);  // #6D417D
        [SerializeField] private Color hoverTextColor = new Color(0.655f, 0.376f, 0.129f, 1f);   // #A76021

        // ===== 이미지 교체 =====

        [Header("이미지 교체")]
        [Tooltip("스프라이트가 바뀔 Image 목록. 각각 normal/hover 스프라이트 지정.")]
        [SerializeField] private ImageSwapEntry[] images;

        private void Awake()
        {
            // texts가 비어있으면 자식에서 자동 검색
            if (texts == null || texts.Length == 0)
            {
                var found = GetComponentInChildren<TMP_Text>();
                if (found != null)
                    texts = new TMP_Text[] { found };
            }

            ApplyNormalState();
        }

        private void OnDisable()
        {
            ApplyNormalState();
        }

        // ===== 이벤트 핸들러 =====

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyHoverState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyNormalState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ApplyHoverState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 버튼 위에서 뗐으면 호버 유지, 밖에서 뗐으면 normal
            // (OnPointerExit가 먼저 호출되므로 여기서는 별도 처리 불필요)
        }

        // ===== 상태 적용 =====

        private void ApplyNormalState()
        {
            if (texts != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null)
                        texts[i].color = normalTextColor;
                }
            }

            if (images != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].target != null && images[i].normalSprite != null)
                        images[i].target.sprite = images[i].normalSprite;
                }
            }
        }

        private void ApplyHoverState()
        {
            if (texts != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null)
                        texts[i].color = hoverTextColor;
                }
            }

            if (images != null)
            {
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].target != null && images[i].hoverSprite != null)
                        images[i].target.sprite = images[i].hoverSprite;
                }
            }
        }

        // ===== 데이터 구조 =====

        [System.Serializable]
        public struct ImageSwapEntry
        {
            [Tooltip("스프라이트가 바뀔 Image")]
            public Image target;
            [Tooltip("기본 상태 스프라이트")]
            public Sprite normalSprite;
            [Tooltip("호버/프레스 시 스프라이트")]
            public Sprite hoverSprite;
        }
    }
}
