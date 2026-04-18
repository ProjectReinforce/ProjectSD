using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Data;
using SwDreams.Shared.Data;

namespace SwDreams.Presentation
{
    /// <summary>
    /// 보스 등장 경고 UI.
    /// BossSpawner.RPC_BossWarning에서 호출.
    ///
    /// 연출:
    ///   1. 화면 전체 빨간 플래시
    ///   2. "보스 등장!" 텍스트 (위에서 바운스)
    ///   3. 보스 혼돈 스킬 표시 (있는 경우)
    ///   4. duration 후 자동 페이드아웃
    ///
    /// 씬에 오브젝트 배치 불필요 — 런타임 자동 생성 + 자동 파괴.
    /// </summary>
    public static class BossWarningUI
    {
        public static void Show(float duration, ChaosEffectType bossChaosType)
        {
            // 캔버스가 있는 곳에 오버레이 생성
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[BossWarningUI] Canvas를 찾을 수 없습니다.");
                return;
            }

            var root = new GameObject("BossWarningOverlay");
            root.transform.SetParent(canvas.transform, false);

            // 풀스크린 RectTransform
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            // 반투명 빨간 배경 플래시
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.6f, 0f, 0f, 0f);
            bg.raycastTarget = false;

            // "보스 등장!" 텍스트
            var textGO = new GameObject("WarningText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(root.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(0f, 30f);
            textRT.sizeDelta = new Vector2(600f, 100f);

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "보스 등장!";
            tmp.fontSize = 56;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.3f, 0.3f, 0f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            // 혼돈 스킬 텍스트 (있는 경우)
            GameObject chaosGO = null;
            if (bossChaosType != ChaosEffectType.None)
            {
                chaosGO = new GameObject("ChaosText", typeof(RectTransform), typeof(TextMeshProUGUI));
                chaosGO.transform.SetParent(root.transform, false);

                var chaosRT = chaosGO.GetComponent<RectTransform>();
                chaosRT.anchorMin = new Vector2(0.5f, 0.5f);
                chaosRT.anchorMax = new Vector2(0.5f, 0.5f);
                chaosRT.pivot = new Vector2(0.5f, 0.5f);
                chaosRT.anchoredPosition = new Vector2(0f, -30f);
                chaosRT.sizeDelta = new Vector2(400f, 50f);

                var chaosTmp = chaosGO.GetComponent<TextMeshProUGUI>();
                chaosTmp.text = $"혼돈 스킬: {GetChaosName(bossChaosType)}";
                chaosTmp.fontSize = 28;
                chaosTmp.alignment = TextAlignmentOptions.Center;
                chaosTmp.color = new Color(1f, 0.6f, 0.2f, 0f);
                chaosTmp.raycastTarget = false;
            }

            // DOTween 연출
            var seq = DOTween.Sequence();

            // 배경 플래시
            seq.Append(bg.DOColor(new Color(0.6f, 0f, 0f, 0.4f), 0.3f));
            seq.Append(bg.DOColor(new Color(0.4f, 0f, 0f, 0.15f), 0.3f));

            // 텍스트: 위에서 바운스 등장
            textRT.anchoredPosition += new Vector2(0f, 80f);
            seq.Join(textRT.DOAnchorPosY(30f, 0.5f).SetEase(Ease.OutBounce));
            seq.Join(tmp.DOFade(1f, 0.3f));

            // 혼돈 스킬 텍스트
            if (chaosGO != null)
            {
                var chaosTmp2 = chaosGO.GetComponent<TextMeshProUGUI>();
                seq.Append(chaosTmp2.DOFade(1f, 0.3f));
            }

            // 유지
            seq.AppendInterval(duration - 1.5f);

            // 페이드아웃
            seq.Append(cg.DOFade(0f, 0.5f));
            seq.OnComplete(() => Object.Destroy(root));
        }

        private static string GetChaosName(ChaosEffectType type)
        {
            return type switch
            {
                ChaosEffectType.GlassCannon => "유리대포",
                ChaosEffectType.ChainExplosion => "연쇄 폭발",
                ChaosEffectType.BerserkMode => "폭주 모드",
                ChaosEffectType.AccelEngine => "가속 엔진",
                ChaosEffectType.Unity => "단결",
                ChaosEffectType.Gambler => "도박꾼",
                _ => ""
            };
        }
    }
}
