namespace Adapter.UI.Menu
{
    /// <summary>
    /// CharacterSelectUI가 선택 결과를 외부에 전달하기 위한 콜백 인터페이스.
    ///
    /// 설계 의도 (DIP - 의존성 역전 원칙):
    ///   CharacterSelectUI는 WaitingRoomPanelController를 직접 참조하지 않고,
    ///   이 인터페이스만 알면 된다. 덕분에:
    ///   - CharacterSelectUI를 다른 화면(예: 메인 로비, 상점)에서 재사용할 수 있다.
    ///   - WaitingRoomPanelController 변경이 CharacterSelectUI에 전파되지 않는다.
    ///   - 테스트 시 Mock 구현으로 쉽게 교체할 수 있다.
    /// </summary>
    public interface ICharacterSelectCallback
    {
        /// <summary>
        /// 캐릭터 선택이 확정(확인 버튼 클릭)되었을 때 호출.
        /// </summary>
        /// <param name="characterId">선택된 캐릭터의 ID</param>
        void OnCharacterConfirmed(int characterId);
    }
}
