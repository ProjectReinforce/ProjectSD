// 자주 쓰는 키 상수 카탈로그. 컴파일타임 안전성용.
// 모든 키를 여기 둘 필요는 없음 — 동적 키($"skill.{id}.name")는 그대로 문자열 사용.
// 신규 키는 시트에 행 추가 → 임포트 → 필요 시 본 파일에 상수 추가.

namespace SwDreams.Shared.Localization.Domain
{
    public static class LocalizationKey
    {
        // ===== UI / Menu =====
        public const string UI_StartButton = "ui.menu.start_button";
        public const string UI_RoomListTitle = "ui.menu.room_list_title";
        public const string UI_SettingsButton = "ui.menu.settings_button";

        // ===== UI / Result =====
        public const string UI_ResultVictory = "ui.result.victory";
        public const string UI_ResultDefeat = "ui.result.defeat";

        // ===== Toast =====
        public const string Toast_Disconnect = "ui.toast.disconnect";
        public const string Toast_HostMigrated = "ui.toast.host_migrated";
        public const string Toast_RoomFull = "ui.toast.room_full";

        // ===== Error =====
        public const string Error_ConnectionLost = "error.connection_lost";
        public const string Error_RoomFull = "error.room_full";

        // ===== Settings =====
        public const string Settings_Title = "ui.settings.title";
        public const string Settings_TabVideo = "ui.settings.tab_video";
        public const string Settings_TabAudio = "ui.settings.tab_audio";
        public const string Settings_TabLanguage = "ui.settings.tab_language";
    }
}
