using SwDreams.Shared.Domain;

namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 플랫폼 SDK 추상화. Local / Stove / Steam 구현체가 따른다.
    /// 호출자는 PlatformBootstrap.Service 를 통해 접근.
    ///
    /// ⚠ Domain 레이어 — UnityEngine / Photon import 절대 금지.
    /// GameResult 만 Shared.Domain 에서 허용.
    /// </summary>
    public interface IPlatformService
    {
        // ===== 라이프사이클 =====
        void Initialize();
        void Shutdown();
        bool IsInitialized { get; }

        // ===== 유저 식별 =====
        /// <summary>
        /// 로컬 유저 정보. SDK 미초기화 시 stub 반환.
        /// PhotonNetwork.LocalPlayer.NickName 을 대체하지 않음 — 추가 경로.
        /// </summary>
        PlatformUserProfile GetLocalUser();

        // ===== 실적 =====
        void UnlockAchievement(string achievementId);
        bool IsAchievementUnlocked(string achievementId);

        // ===== 통계 =====
        /// <summary>누적 통계. Steam 의 SetStat(... + delta) 와 동등.</summary>
        void IncrementStat(string statId, int delta);

        /// <summary>한 판 결과 제출. Stove/Steam 의 리더보드/통계 API 로 매핑.</summary>
        void SubmitRunResult(GameResult result);

        // ===== 클라우드 세이브 =====
        /// <summary>메타 진행도 저장 (key=설정 키, json=직렬화된 페이로드).</summary>
        void SaveData(string key, string json);

        /// <summary>저장된 데이터 로드. 없으면 null 반환.</summary>
        string LoadData(string key);
    }
}
