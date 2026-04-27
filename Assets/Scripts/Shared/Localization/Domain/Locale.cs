namespace SwDreams.Shared.Localization.Domain
{
    /// <summary>
    /// 1차 지원 언어 4종. 클라이언트 로컬 (네트워크 동기화 안 함 — 같은 룸에서 각자 다른 언어 가능).
    ///
    /// PlayerPrefs/클라우드에 (int) 로 직렬화되므로 enum 순서 변경 금지.
    /// 추가 언어는 Localization Phase D 이후 검토. 7개 이상 + Pluralization 필요 시 Unity Localization Package 마이그레이션.
    ///
    /// 본 파일은 Localization Phase A (Domain) 의 첫 산출물 — Settings 패널 (R12) 이 선행 사용.
    /// </summary>
    public enum Locale
    {
        KO_KR = 0,
        EN_US = 1,
        JA_JP = 2,
        ZH_CN = 3,
    }
}
