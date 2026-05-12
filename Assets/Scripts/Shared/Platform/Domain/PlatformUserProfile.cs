namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 플랫폼 유저 정보 VO. 순수 C#.
    /// Stove/Steam SDK 도입 시 각 SDK 의 유저 ID/닉네임을 이 형태로 정규화.
    /// </summary>
    public class PlatformUserProfile
    {
        /// <summary>플랫폼 전역 ID. Steam ID / Stove ID / "local-{photonActor}".</summary>
        public string UserId { get; set; }

        /// <summary>표시용 닉네임.</summary>
        public string DisplayName { get; set; }

        /// <summary>이 프로필이 어디서 왔는가.</summary>
        public PlatformType Source { get; set; }
    }
}
