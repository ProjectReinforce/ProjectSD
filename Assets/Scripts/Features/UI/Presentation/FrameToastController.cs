using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// FrameToast 프리팹을 구동하는 단일 인스턴스 컨트롤러.
    ///
    /// 배치 규칙:
    ///   MenuScene 의 DontDestroyOnLoad 시스템 오브젝트 자식 Canvas 아래에
    ///   FrameToast 프리팹 인스턴스 1개를 두고, 본 컴포넌트를 그 루트에 부착한다.
    ///   씬 전환 후에도 동일 인스턴스를 재사용하므로 GameScene 에서도 그대로 호출 가능.
    ///
    /// 사용:
    ///   FrameToastController.Show("이미 존재하는 방 이름입니다");
    ///   FrameToastController.Show("저장 완료", duration: 1.5f);
    ///
    /// 일시정지(GameState.Paused) 중에도 안내가 동작해야 하므로 Time.unscaledTime 사용.
    /// </summary>
    public class FrameToastController : MonoBehaviour
    {
        private static FrameToastController instance;

        [Header("References")]
        [Tooltip("표시/숨김 토글 대상. 보통 FrameToast 루트의 자식 'Frame' 오브젝트.")]
        [SerializeField] private GameObject rootObject;

        [Tooltip("토스트 메시지를 표시할 TMP Text. FrameToast/Frame/Text.")]
        [SerializeField] private TextMeshProUGUI messageText;

        [Tooltip("페이드 인/아웃을 적용할 CanvasGroup. 없으면 페이드 생략하고 즉시 토글한다.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing")]
        [Tooltip("자동으로 닫히기까지의 시간(초).")]
        [SerializeField] private float defaultDuration = 3f;

        [Tooltip("표시 직후 입력 무시 시간(초). 직전 화면의 클릭/키 잔여가 토스트를 즉시 닫는 사고를 막는다.")]
        [SerializeField] private float inputGracePeriod = 0.3f;

        [Tooltip("페이드 인/아웃 시간(초). 0 이하면 즉시 표시/숨김.")]
        [SerializeField] private float fadeDuration = 0.2f;

        private enum FadePhase { None, FadingIn, Visible, FadingOut }

        private bool isShowing;
        private float showStartUnscaledTime;
        private float autoCloseUnscaledTime;
        private float fadePhaseStartUnscaledTime;
        private FadePhase fadePhase;

        public static FrameToastController Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// 토스트 표시. 이미 떠 있으면 메시지를 교체하고 타이머/그레이스 기간을 리셋한다.
        /// duration 이 0 이하이면 인스펙터의 defaultDuration 사용.
        /// </summary>
        public static void Show(string message, float duration = -1f)
        {
            if (instance == null)
            {
                Debug.LogWarning($"[FrameToast] Instance is null — drop: {message}");
                return;
            }
            instance.ShowInternal(message, duration);
        }

        private void ShowInternal(string message, float duration)
        {
            if (messageText != null) messageText.text = message;
            if (rootObject != null) rootObject.SetActive(true);

            float now = Time.unscaledTime;
            showStartUnscaledTime = now;
            autoCloseUnscaledTime = now + (duration > 0f ? duration : defaultDuration);

            fadePhaseStartUnscaledTime = now;
            if (fadeDuration > 0f && canvasGroup != null)
            {
                fadePhase = FadePhase.FadingIn;
                canvasGroup.alpha = 0f;
            }
            else
            {
                fadePhase = FadePhase.Visible;
                if (canvasGroup != null) canvasGroup.alpha = 1f;
            }

            isShowing = true;
        }

        private void Update()
        {
            if (!isShowing) return;

            float now = Time.unscaledTime;
            UpdateFade(now);

            // 그레이스 기간이 지나면 임의 입력으로 닫기 허용.
            if (fadePhase != FadePhase.FadingOut &&
                now - showStartUnscaledTime >= inputGracePeriod &&
                IsDismissInputPressed())
            {
                BeginClose(now);
                return;
            }

            // 자동 닫힘.
            if (fadePhase != FadePhase.FadingOut && now >= autoCloseUnscaledTime)
            {
                BeginClose(now);
            }
        }

        /// <summary>
        /// 키보드 아무 키 또는 마우스 좌/우/중클릭이 이번 프레임에 눌렸는지.
        /// 게임패드는 의도적으로 제외 — 게임씬에서 패드로 조작 중에 토스트가 임의로 닫히는 사고를 막기 위함.
        /// 필요해지면 Gamepad.current.startButton 등으로 명시 추가.
        /// </summary>
        private static bool IsDismissInputPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                                  mouse.rightButton.wasPressedThisFrame ||
                                  mouse.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        private void UpdateFade(float now)
        {
            if (canvasGroup == null || fadeDuration <= 0f) return;

            float elapsed = now - fadePhaseStartUnscaledTime;
            switch (fadePhase)
            {
                case FadePhase.FadingIn:
                    if (elapsed >= fadeDuration)
                    {
                        canvasGroup.alpha = 1f;
                        fadePhase = FadePhase.Visible;
                    }
                    else
                    {
                        canvasGroup.alpha = elapsed / fadeDuration;
                    }
                    break;

                case FadePhase.FadingOut:
                    if (elapsed >= fadeDuration)
                    {
                        canvasGroup.alpha = 0f;
                        FinishClose();
                    }
                    else
                    {
                        canvasGroup.alpha = 1f - (elapsed / fadeDuration);
                    }
                    break;
            }
        }

        private void BeginClose(float now)
        {
            if (fadeDuration > 0f && canvasGroup != null)
            {
                fadePhase = FadePhase.FadingOut;
                fadePhaseStartUnscaledTime = now;
            }
            else
            {
                FinishClose();
            }
        }

        private void FinishClose()
        {
            isShowing = false;
            fadePhase = FadePhase.None;
            if (rootObject != null) rootObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
