namespace SwDreams.Shared.Domain
{
    /// <summary>
    /// 게임 난이도. 방 생성 시 호스트가 선택하여 Room.CustomProperties 에 저장.
    /// SpawnManager 가 게임씬 진입 시 읽어 DifficultyManager 의 배율로 사용.
    ///
    /// 순수 C# — Unity 의존 없음. 정수 ID 로 직렬화 (CustomProperties / RPC payload).
    /// </summary>
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }
}
