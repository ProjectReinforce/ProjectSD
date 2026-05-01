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
    /// 사용:
    ///   confirmDialog.Show("제목", "메시지", () => DoSomething());
    ///   취소 버튼 / Cancel() 호출 시 onConfirm 미발화.
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
            canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);

            if (btnConfirm != null) btnConfirm.onClick.AddListener(Confirm);
            if (btnCancel != null) btnCancel.onClick.AddListener(Cancel);
        }

        private void OnDestroy()
        {
            if (btnConfirm != null) btnConfirm.onClick.RemoveListener(Confirm);
            if (btnCancel != null) btnCancel.onClick.RemoveListener(Cancel);
        }

        public void Show(string title, string message, Action onConfirm)
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
