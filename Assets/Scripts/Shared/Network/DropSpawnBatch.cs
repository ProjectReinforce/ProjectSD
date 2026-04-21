namespace SwDreams.Shared.Network
{
    /// <summary>
    /// 드랍 스폰 배치 전송 포맷 SSOT.
    ///
    /// RaiseEvent 채널: <see cref="EventCode"/>
    /// Payload: float[] — <see cref="Stride"/> 개 원소 단위로 반복.
    ///   [0] typeInt  (PickupType enum 값)
    ///   [1] x
    ///   [2] y
    ///   [3] rarityInt (Rarity enum 값)
    ///   [4] dataIdHash (string itemId 의 stable hash. 0 이면 random/default)
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
}
