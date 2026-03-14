using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace SwDreams.Presentation
{
    /// <summary>
    /// 호스트 재연결 대기 UI.
    /// "호스트 재연결 대기 중... X초" + 깜빡이는 경고 아이콘.
    ///
    /// HostMigrationHandler 이벤트 구독.
    ///
    /// 셋업:
    /// - Canvas 하위에 "ReconnectOverlay" 오브젝트 생성
    /// - CanvasGroup + 이 스크립트 부착
    /// - 자식: TMP_Text (timerText) + Image (warningIcon)
    /// - 비활성 상태로 시작
    /// </summary>
    public class ReconnectUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image warningIcon;

        [Header("연출")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private Color warningColorA = Color.yellow;
        [SerializeField] private Color warningColorB = Color.red;
        [SerializeField] private float blinkSpeed = 2f;

        private Tween fadeTween;
        private bool isShowing = false;

        private void Start()
        {
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.blocksRaycasts = false;
            }
            gameObject.SetActive(false);

            TrySubscribe();
        }

        private bool subscribed = false;

        private void TrySubscribe()
        {
            var handler = Adapter.Manager.HostMigrationHandler.Instance;
            if (handler != null && !subscribed)
            {
                handler.OnMigrationStarted += Show;
                handler.OnMigrationCompleted += Hide;
                handler.OnReconnectTimerUpdated += UpdateTimer;
                subscribed = true;
            }
        }

        private void Update()
        {
            // 구독 실패했으면 매 프레임 재시도
            if (!subscribed) TrySubscribe();

            if (!isShowing) return;

            // 경고 아이콘 깜빡임
            if (warningIcon != null)
            {
                float t = Mathf.PingPong(Time.unscaledTime * blinkSpeed, 1f);
                warningIcon.color = Color.Lerp(warningColorA, warningColorB, t);
            }
        }

        private void OnDestroy()
        {
            fadeTween?.Kill();
            if (subscribed)
            {
                var handler = Adapter.Manager.HostMigrationHandler.Instance;
                if (handler != null)
                {
                    handler.OnMigrationStarted -= Show;
                    handler.OnMigrationCompleted -= Hide;
                    handler.OnReconnectTimerUpdated -= UpdateTimer;
                }
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            isShowing = true;

            if (messageText != null)
                messageText.text = "호스트 연결 끊김";

            if (timerText != null)
                timerText.text = "";

            fadeTween?.Kill();
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.blocksRaycasts = true;
                fadeTween = overlay.DOFade(0.8f, fadeInDuration)
                    .SetUpdate(true);
            }
        }

        public void Hide()
        {
            isShowing = false;

            fadeTween?.Kill();
            if (overlay != null)
            {
                fadeTween = overlay.DOFade(0f, 0.2f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        overlay.blocksRaycasts = false;
                        gameObject.SetActive(false);
                    });
            }
        }

        private void UpdateTimer(float remaining, float total)
        {
            if (timerText == null) return;

            if (remaining <= 0f)
                timerText.text = "새 호스트 전환 중...";
            else
                timerText.text = $"재연결 대기 중... {Mathf.CeilToInt(remaining)}초";
        }
    }
}