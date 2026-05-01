// 다국어 텍스트 조회의 핵심 인터페이스.
// CLAUDE.md §2 — Domain 레이어는 UnityEngine / Photon / TMPro import 금지.
// 백엔드 교체(Unity Localization Package 등) 시 본 인터페이스만 만족하면 호출부 무수정.

using System;

namespace SwDreams.Shared.Localization.Domain
{
    public interface ILocalizationService
    {
        Locale CurrentLocale { get; }

        /// <summary>현재 Locale 의 번역 텍스트. 키 누락 시 키 자체 반환 (디버깅 용이성).</summary>
        string Get(string key);

        /// <summary>특정 Locale 강제 조회 (튜토리얼 미리보기 등 특수 용도).</summary>
        string Get(string key, Locale locale);

        /// <summary>named placeholder 치환. C# string.Format 의 {0} 인덱스 형식 금지 — 언어별 어순 차이로 깨짐.</summary>
        string GetFormat(string key, params (string name, object value)[] args);

        void SetLocale(Locale locale);

        /// <summary>Locale 변경 시 발생. LocalizedText 가 구독.</summary>
        event Action OnLocaleChanged;
    }
}
