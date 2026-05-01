namespace SwDreams.Features.Voice.Domain
{
    /// <summary>
    /// R3 마이크 필터 드랍 아이템에서 적용 가능한 필터 종류.
    /// 카오스 재미 위주 — 본인은 자기 음성을 못 듣고(Photon Voice self-mute) 다른 사람 화면에서만 변형됨.
    ///
    /// PlayerPrefs/네트워크에 (int) 로 직렬화되므로 enum 순서 변경 금지.
    /// 추가 시 enum 끝에 append + Database SO 에 신규 SO 추가.
    /// </summary>
    public enum MicFilterType
    {
        LowPass = 0,        // 먹먹/물 속 — AudioLowPassFilter
        Distortion = 1,     // 깨진 마이크/지지직 — AudioDistortionFilter
        Echo = 2,           // 동굴 메아리 — AudioEchoFilter
        PitchHelium = 3,    // 헬륨 고음 — AudioSource.pitch 1.5+
        PitchDemon = 4,     // 악마 저음 — AudioSource.pitch 0.6
    }
}
