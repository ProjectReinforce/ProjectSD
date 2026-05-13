using Photon.Realtime;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// LobbyPlayerEntry 의 Kick 버튼 클릭을 외부 컨트롤러에 위임하기 위한 콜백 인터페이스.
    ///
    /// 설계 의도 (DIP):
    ///   LobbyPlayerEntry 는 WaitingRoomPanelController 를 직접 참조하지 않고
    ///   이 인터페이스만 안다. 강퇴 흐름(확인 다이얼로그 표시 → NetworkManager.KickPlayer)은
    ///   호출자(WaitingRoomPanelController)가 결정.
    /// </summary>
    public interface IKickRequestHandler
    {
        /// <summary>지정 플레이어 강퇴 요청. 구현체가 확인 다이얼로그/실제 강퇴 호출 책임을 진다.</summary>
        void RequestKick(Player player);
    }
}
