namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 플랫폼 종류. PlatformBootstrap 이 어떤 IPlatformService 구현체를 생성할지 결정.
    /// Phase A 에서는 Local 만 동작. Stove/Steam 은 Phase B/C 에서 추가.
    /// </summary>
    public enum PlatformType
    {
        Local = 0,   // Editor / 개발용 stub (PlayerPrefs 백엔드)
        Stove = 1,   // Phase B
        Steam = 2,   // Phase C
    }
}
