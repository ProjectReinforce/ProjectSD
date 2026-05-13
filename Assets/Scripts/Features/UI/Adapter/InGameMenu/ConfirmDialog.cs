using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SwDreams.Features.UI.Adapter.InGameMenu
{
    /// <summary>
    /// 임시 모달 확인 다이얼로그. Frame_PopUp 미작성 상태의 stand-in.
    /// 작성되면 [docs/systems/ui-frame.md] 의 Frame_PopUp 으로 일괄 이관.
    ///
    /// 사용 (정적 — 권장):
    ///   ConfirmDialog.Show("제목", "메시지", () => DoSomething());
    ///   취소 버튼 / Cancel() 호출 시 onConfirm 미발화.
    ///
    /// 배치 규칙:
    ///   FrameToastController 와 동일하게 메뉴씬 DontDestroyOnLoad 시스템 오브젝트 자식 Canvas 아래에
    ///   인스턴스 1개를 두고 싱글톤으로 운영. 메뉴씬/게임씬 어디서든 정적 호출 가능.
    ///
    /// Hierarchy (사용자 prefab 작업 가이드):
    ///   ConfirmDialog (CanvasGroup + 본 스크립트)
    ///   ├─ Background (Image 반투명, full screen, raycast block)
    ///   └─ Card (중앙 모달)
    ///       ├─ Title (TMP_Text)
    ///       ├─ Message (TMP_Text)
    ///       ├─ Btn_Confirm
    ///       └─ Btn_Cancel
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ConfirmDialog : MonoBehaviour
    {
        private static ConfirmDialog instance;
        public static ConfirmDialog Instance => instance;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        [Header("Buttons")]
        [SerializeField] private Button btnConfirm;
        [SerializeField] private Button btnCancel;

        private CanvasGroup canvasGroup;
        private Action pendingConfirm;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);

            if (btnConfirm != null) btnConfirm.onClick.AddListener(Confirm);
            if (btnCancel != null) btnCancel.onClick.AddListener(Cancel);
        }

        private void OnDestroy()
        {
            if (btnConfirm != null) btnConfirm.onClick.RemoveListener(Confirm);
            if (btnCancel != null) btnCancel.onClick.RemoveListener(Cancel);
            if (instance == this) instance = null;
        }

        /// <summary>정적 호출 — 글로벌 싱글톤 인스턴스에 위임. 인스턴스 없으면 경고 후 즉시 확인 콜백 실행 (안전 fallback).</summary>
        public static void Show(string title, string message, Action onConfirm)
        {
            if (instance == null)
            {
                Debug.LogWarning($"[ConfirmDialog] Instance is null — 즉시 확인 처리: {title}");
                onConfirm?.Invoke();
                return;
            }
            instance.ShowInternal(title, message, onConfirm);
        }

        private void ShowInternal(string title, string message, Action onConfirm)
        {
            pendingConfirm = onConfirm;
            if (titleText != null) titleText.text = title ?? string.Empty;
            if (messageText != null) messageText.text = message ?? string.Empty;

            SetVisible(true);
            isOpen = true;
        }

        public void Confirm()
        {
            if (!isOpen) return;
            var cb = pendingConfirm;
            pendingConfirm = null;
            isOpen = false;
            SetVisible(false);
            cb?.Invoke();
        }

        public void Cancel()
        {
            if (!isOpen) return;
            pendingConfirm = null;
            isOpen = false;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            // 닫을 때 EventSystem 의 last-selected 가 보이지 않는 버튼에 남아 Submit/ESC 가 잘못 라우팅되는 것 방지.
            if (!visible && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
