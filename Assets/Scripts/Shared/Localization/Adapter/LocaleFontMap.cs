using TMPro;
using UnityEngine;
using SwDreams.Shared.Localization.Domain;

namespace SwDreams.Shared.Localization.Adapter
{
    /// <summary>
    /// Locale ↔ TMP_FontAsset 매핑. LocalizedText 가 Locale 변경 시 폰트도 함께 교체.
    /// Phase D 에서 NotoSans 4종 셋업 후 인스펙터에 할당. 미할당 슬롯은 fallback (EN → 미설정 시 null).
    /// </summary>
    [CreateAssetMenu(fileName = "LocaleFontMap",
                     menuName = "ProjectSD/Data/LocaleFontMap")]
    public class LocaleFontMap : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset koFont;
        [SerializeField] private TMP_FontAsset enFont;
        [SerializeField] private TMP_FontAsset jaFont;
        [SerializeField] private TMP_FontAsset zhFont;

        public TMP_FontAsset GetFont(Locale locale)
        {
            var picked = locale switch
            {
                Locale.KO_KR => koFont,
                Locale.EN_US => enFont,
                Locale.JA_JP => jaFont,
                Locale.ZH_CN => zhFont,
                _ => enFont,
            };
            return picked != null ? picked : enFont;
        }
    }
}
