using Photon.Realtime;
using SwDreams.Features.UI.Adapter.Menu;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 방 리스트 아이템의 클릭 이벤트를 처리하는 인터페이스.
    ///
    /// 설계 의도:
    ///   RoomListItem은 "자신이 클릭되었다"는 사실만 알리고,
    ///   실제 처리(비밀번호 확인, 방 진입 등)는 이 인터페이스를 구현한 쪽이 담당한다.
    ///   이렇게 하면 RoomListItem이 RoomListPanelController에 직접 의존하지 않으므로,
    ///   다른 화면(예: 친구 초대 UI)에서도 동일한 아이템 프리팹을 재사용할 수 있다.
    /// </summary>
    public interface IRoomListItemHandler
    {
        void OnRoomItemClicked(RoomInfo roomInfo);
    }
}
