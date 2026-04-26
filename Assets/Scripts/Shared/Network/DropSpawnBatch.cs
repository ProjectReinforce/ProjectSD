namespace SwDreams.Shared.Network
{
    /// <summary>
    /// 드랍 스폰 배치 전송 포맷 SSOT.
    ///
    /// RaiseEvent 채널: <see cref="EventCode"/>
    /// Payload: float[] — <see cref="Stride"/> 개 원소 단위로 반복.
    ///   [0] typeInt    (PickupType enum 값)
    ///   [1] x
    ///   [2] y
    ///   [3] rarityInt  (Rarity enum 값. 무기/혼돈/능력치 등 4등급 체계에만 의미.
    ///                   정수/자석/물약은 Common(0) 고정.)
    ///   [4] dataIdHash (다용도 보조 인덱스.
    ///                   - Essence: 속성 타입 인덱스 (0=Ice, 1=Fire, 2=Lightning)
    ///                   - Weapon: string itemId 의 stable hash (추후 Phase 4)
    ///                   - 기타: 0)
    /// </summary>
    public static class DropSpawnBatch
    {
        public const byte EventCode = 13;
        public const int Stride = 5;

        public const int IdxType = 0;
        public const int IdxPosX = 1;
        public const int IdxPosY = 2;
        public const int IdxRarity = 3;
        public const int IdxDataIdHash = 4;
    }

    /// <summary>
    /// 호스트 측 픽업 처리 후 다른 클라에 풀 반환 알림 — PickupItemBase 자체엔 PhotonView 가 없어
    /// DropSpawner 의 RaiseEvent 인프라를 재사용. payload = object[] { float x, float y, string itemId }.
    /// </summary>
    public static class PickupCollectedEvent
    {
        public const byte EventCode = 14;
    }

    /// <summary>
    /// [B8 A-1] 호스트가 모든 클라에 GameScene 로드 신호 송신.
    /// AutomaticallySyncScene = false 정책상 PhotonNetwork.LoadLevel 대신 명시적 RaiseEvent 사용.
    /// payload = string sceneName. NetworkManager 가 송수신.
    /// </summary>
    public static class LoadSceneEvent
    {
        public const byte EventCode = 15;
    }

    /// <summary>
    /// [B8] 대기실(WaitingRoomPanel) 진입 시 다른 클라들에 자기 LobbyPlayer 재 spawn 요청.
    /// PhotonNetwork.Instantiate 의 buffered event 가 같은 클라에 replay 되지 않으므로
    /// 늦게 진입한 측이 먼저 진입자의 LobbyPlayer 를 받지 못함 → 모두 새로 spawn 해 새 buffered event 보장.
    /// payload 없음. LobbyPlayerSpawner 가 송수신.
    /// </summary>
    public static class LobbyRefreshEvent
    {
        public const byte EventCode = 16;
    }
}
