using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 강퇴된 본인의 메뉴씬 자동 라우팅 + 토스트 안내를 담당하는 글로벌 핸들러.
    ///
    /// 배치 규칙:
    ///   FrameToastController / ConfirmDialog 와 동일하게 메뉴씬 DontDestroyOnLoad 시스템 오브젝트 자식에 부착.
    ///   메뉴씬/게임씬 양쪽에서 살아있어야 게임 중 강퇴도 처리 가능.
    ///
    /// 동작:
    ///   ① NetworkManager.WasKicked 이벤트 (호스트가 RaiseEvent 로 보낸 명시 신호) — 1순위.
    ///   ② OnDisconnected(DisconnectByServerLogic) 안전망 — RaiseEvent 가 도달 안 한 케이스 방어.
    ///   감지 즉시 토스트 표시 + 메뉴씬 로드 + ReturnToRoomList=true (대기실 자동 진입 방지).
    ///   중복 처리 방지 가드 (한 번만 라우팅).
    /// </summary>
    public class KickedRouter : MonoBehaviour
    {
        private static KickedRouter instance;

        [Tooltip("메뉴씬 이름 — SceneManager.LoadScene 호출 시 사용.")]
        [SerializeField] private string menuSceneName = "MenuScene";

        private bool isRoutingToMenu;
        private NetworkManagerCallbacks callbacks;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.WasKicked += HandleWasKicked;
            }

            // PUN 콜백을 직접 받으려면 MonoBehaviourPunCallbacks 가 필요하지만,
            // KickedRouter 자체를 PunCallbacks 로 만들면 메뉴씬 진입 시점 race 가 복잡해진다.
            // 자식 GameObject 에 콜백 전용 컴포넌트를 붙여 OnDisconnected 안전망만 위임받는다.
            callbacks = gameObject.AddComponent<NetworkManagerCallbacks>();
            callbacks.OnDisconnectedHandler = OnPhotonDisconnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.WasKicked -= HandleWasKicked;
            }
            if (instance == this) instance = null;
        }

        private void HandleWasKicked()
        {
            RouteToMenu();
        }

        private void OnPhotonDisconnected(DisconnectCause cause)
        {
            // 호스트가 보낸 강퇴 RaiseEvent 가 도달 안 한 안전망 — DisconnectByServerLogic 만 강퇴로 간주.
            // (다른 서버 정책 종료도 같은 cause 일 수 있어 약간 가짜양성 가능 — 안내 메시지만 다르게 처리)
            if (cause == DisconnectCause.DisconnectByServerLogic)
            {
                RouteToMenu();
            }
        }

        private void RouteToMenu()
        {
            if (isRoutingToMenu) return;
            isRoutingToMenu = true;

            FrameToastController.Show("방장에 의해 강퇴되었습니다");

            // 메뉴씬 진입 후 대기실로 자동 입장하지 않도록 ReturnToRoomList 플래그를 set.
            // MenuSceneManager.Start 가 이 플래그를 보고 ShowRoomList 로 분기.
            SwDreams.Features.UI.Adapter.Menu.MenuSceneManager.ReturnToRoomList = true;

            // 이미 메뉴씬이면 굳이 재로드 안 함 (대기실에서 강퇴된 케이스 — 자체 LeftRoom 흐름이 처리).
            var current = SceneManager.GetActiveScene().name;
            if (!string.Equals(current, menuSceneName, System.StringComparison.Ordinal))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        /// <summary>
        /// PUN 콜백을 KickedRouter 가 직접 상속하지 않고 분리하기 위한 얇은 자식 컴포넌트.
        /// MonoBehaviourPunCallbacks 를 상속하면 자동으로 PUN 콜백 시스템에 등록된다.
        /// </summary>
        private class NetworkManagerCallbacks : MonoBehaviourPunCallbacks
        {
            public System.Action<DisconnectCause> OnDisconnectedHandler;

            public override void OnDisconnected(DisconnectCause cause)
            {
                OnDisconnectedHandler?.Invoke(cause);
            }
        }
    }
}
