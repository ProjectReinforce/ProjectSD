using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using SwDreams.Features.Pickup.Adapter;

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

        private void OnEnable()
        {
            HideImmediate();
        }

        private void Update()
        {
            if (boundInteractor == null)
            {
                TryBindLocalPlayer();
                return;
            }
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
