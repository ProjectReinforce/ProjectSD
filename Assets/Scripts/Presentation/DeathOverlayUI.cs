using UnityEngine;
using TMPro;
using DG.Tweening;
using Photon.Pun;
using SwDreams.Adapter.Manager;

namespace SwDreams.Presentation
{
    /// <summary>
    /// 사망 시 화면 오버레이 + 부활 카운트다운.
    ///
    /// 로컬 PlayerStub의 OnDied / OnRespawned 이벤트 구독.
    /// RespawnManager.OnLocalRespawnTimer로 카운트다운 갱신.
    ///
    /// 셋업:
    /// - Canvas 하위에 "DeathOverlay" 오브젝트 생성
    /// - CanvasGroup (alpha=0, blocksRaycasts=false)
    /// - 자식: 카운트다운 TMP_Text + 메시지 TMP_Text
    /// - 이 스크립트 부착
    /// - 비활성 상태로 시작
    /// </summary>
    public class DeathOverlayUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text messageText;

        [Header("연출")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        private Tween fadeTween;
        private SwDreams.Testing.PlayerStub localPlayerStub;
        private bool isSubscribed;

        private bool isRespawnSubscribed;

        private void Start()
        {
            // 초기 상태: 비주얼만 숨김 (Update는 동작해야 자동 감지 가능)
            if (overlay != null)
            {
                overlay.alpha = 0f;
                overlay.blocksRaycasts = false;
            }

            TrySubscribeRespawnManager();
        }

        private void Update()
        {
            // 로컬 PlayerStub 자동 감지 + 이벤트 구독
            if (!isSubscribed)
                TrySubscribeToLocalPlayer();

            // RespawnManager가 Start 시점에 없었으면 재시도
            if (!isRespawnSubscribed)
                TrySubscribeRespawnManager();
        }

        private void TrySubscribeRespawnManager()
        {
            if (isRespawnSubscribed) return;
            if (RespawnManager.Instance != null)
            {
                RespawnManager.Instance.OnLocalRespawnTimer += UpdateCountdown;
                isRespawnSubscribed = true;
            }
        }

        private void TrySubscribeToLocalPlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    localPlayerStub = p.GetComponent<SwDreams.Testing.PlayerStub>();
                    if (localPlayerStub != null)
                    {
                        localPlayerStub.OnDied += ShowDeath;
                        localPlayerStub.OnRespawned += HideDeath;
                        isSubscribed = true;
                        Debug.Log("[DeathOverlayUI] 로컬 플레이어 이벤트 구독 완료");
                    }
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            fadeTween?.Kill();
            if (RespawnManager.Instance != null)
                RespawnManager.Instance.OnLocalRespawnTimer -= UpdateCountdown;

            if (localPlayerStub != null)
            {
                localPlayerStub.OnDied -= ShowDeath;
                localPlayerStub.OnRespawned -= HideDeath;
            }
        }

        /// <summary>
        /// 사망 시 호출. PlayerStub.OnDied에서 연결.
        /// </summary>
        public void ShowDeath()
        {
            gameObject.SetActive(true);

            if (messageText != null)
                messageText.text = "사망!";

            if (countdownText != null)
                countdownText.text = "";

            fadeTween?.Kill();
            if (overlay != null)
            {
                overlay.alpha = 0f;
                fadeTween = overlay.DOFade(0.7f, fadeInDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true); // timeScale=0에서도 동작
            }
        }

        /// <summary>
        /// 부활 시 호출. PlayerStub.OnRespawned에서 연결.
        /// </summary>
        public void HideDeath()
        {
            fadeTween?.Kill();
            if (overlay != null)
            {
                fadeTween = overlay.DOFade(0f, fadeOutDuration)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        overlay.blocksRaycasts = false;
                        gameObject.SetActive(false);
                    });
            }
        }

        /// <summary>
        /// 전원 사망 시 호출. "게임 오버" 표시.
        /// </summary>
        public void ShowGameOver()
        {
            gameObject.SetActive(true);

            if (messageText != null)
                messageText.text = "게임 오버";

            if (countdownText != null)
                countdownText.text = "";

            fadeTween?.Kill();
            if (overlay != null)
            {
                overlay.alpha = 0f;
                fadeTween = overlay.DOFade(0.9f, fadeInDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        private void UpdateCountdown(float remaining, float total)
        {
            if (countdownText == null) return;

            if (remaining <= 0f)
            {
                countdownText.text = "부활!";
                return;
            }

            countdownText.text = $"부활까지 {Mathf.CeilToInt(remaining)}초...";
        }
    }
}