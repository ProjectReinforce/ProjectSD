using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Pickup.Presentation
{
    /// <summary>
    /// 상호작용 프롬프트 HUD. 로컬 플레이어의 PlayerPickupInteractor 를 구독해
    /// 근처 상호작용 가능한 픽업이 있으면 "SPACE: 획득" 같은 힌트를 표시.
    ///
    /// 구성:
    /// - root: 전체 패널 (SetActive 로 show/hide)
    /// - keyLabel: "SPACE" 고정 텍스트
    /// - actionLabel: PickupItemBase.PromptActionLabel ("정수 획득" / "무기 획득" / "조합" 등)
    /// - grayOverlay: 획득 불가 상태(정수 2슬롯 가득 등) 시 활성화되는 회색 오버레이
    /// - extraInfoLabel: PickupItemBase.PromptExtraInfo (무기 조합 프리뷰 등, 있을 때만 표시)
    ///
    /// InGameHUD 와 같은 Canvas 하위에 배치. Inspector 에서 위 필드 연결.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private GameObject grayOverlay;
        [SerializeField] private TMP_Text extraInfoLabel;

        [Header("설정")]
        [SerializeField] private string keyText = "SPACE";

        private PlayerPickupInteractor boundInteractor;

        // GameState 전이 감지용 — Paused 진입 시 prompt 즉시 숨김 (LevelUpPanel 위에 노출 방지).
        private bool gameplayStatePrev = true;

        private void OnEnable()
        {
            HideImmediate();
            // 폴링 비교 baseline 을 현재 상태로 초기화 — 첫 프레임 가짜 전이 방지.
            gameplayStatePrev = IsGameplayState();
        }

        private void Update()
        {
            if (boundInteractor == null)
            {
                TryBindLocalPlayer();
                return;
            }

            // GameState 전이 시 강제 갱신 — Refresh 는 OnTargetChanged 에만 묶여 있어
            // Paused 진입 같은 상태 변경을 자체 감지 못 함. 폴링으로 보강.
            bool gameplayNow = IsGameplayState();
            if (gameplayNow != gameplayStatePrev)
            {
                gameplayStatePrev = gameplayNow;
                Refresh();
            }
        }

        private static bool IsGameplayState()
        {
            var state = GameManager.Instance?.CurrentState;
            return state == GameManager.GameState.Playing ||
                   state == GameManager.GameState.BossFight;
        }

        private void OnDestroy()
        {
            if (boundInteractor != null)
                boundInteractor.OnTargetChanged -= Refresh;
        }

        private void TryBindLocalPlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                var pv = players[i].GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine) continue;

                var interactor = players[i].GetComponentInChildren<PlayerPickupInteractor>();
                if (interactor == null) continue;

                boundInteractor = interactor;
                boundInteractor.OnTargetChanged += Refresh;
                Refresh();
                return;
            }
        }

        private void Refresh()
        {
            if (boundInteractor == null || boundInteractor.CurrentTarget == null)
            {
                HideImmediate();
                return;
            }

            // 일시정지(레벨업/메뉴) / 비전투 상태에선 prompt 숨김 — LevelUpPanel 위에 떠보이는 문제 방지.
            if (!IsGameplayState())
            {
                HideImmediate();
                return;
            }

            if (root != null) root.SetActive(true);

            var target = boundInteractor.CurrentTarget;
            if (keyLabel != null) keyLabel.text = keyText;
            if (actionLabel != null) actionLabel.text = target.PromptActionLabel;
            if (grayOverlay != null) grayOverlay.SetActive(!boundInteractor.CurrentTargetPickupable);

            if (extraInfoLabel != null)
            {
                string extra = target.PromptExtraInfo;
                bool hasExtra = !string.IsNullOrEmpty(extra);
                extraInfoLabel.gameObject.SetActive(hasExtra);
                extraInfoLabel.text = hasExtra ? extra : string.Empty;
            }
        }

        private void HideImmediate()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
