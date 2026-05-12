using System;
using System.Collections.Generic;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Unlock.Adapter;
using SwDreams.Features.Unlock.Domain;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Shared.Domain;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 결과 화면 UI.
    /// GameClear/GameOver 시 ResultManager가 UIManager.ShowResult()로 표시.
    ///
    /// 표시 내용:
    /// - 클리어/실패 타이틀
    /// - 팀 통계 (플레이 타임, 레벨, 처치 수, 사망 횟수)
    /// - 플레이어별 빌드 요약 (장착 스킬, 혼돈 스킬)
    /// - 보스 혼돈 스킬
    /// - 다시 하기 / 나가기 버튼
    ///
    /// 셋업: ResultPanel 오브젝트에 부착.
    /// UIManager의 resultPanel 필드에 연결.
    /// Canvas 하위에 배치 (전체화면 오버레이).
    ///
    /// UI 요소는 런타임 자동 생성. 인스펙터 연결 불필요.
    /// </summary>
    public class ResultPanelUI : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float animDuration = 0.5f;

        // 런타임 생성 UI 요소
        private CanvasGroup canvasGroup;
        private TMP_Text titleText;
        private TMP_Text statsText;
        private TMP_Text buildsText;
        private TMP_Text bossChaosText;
        private TMP_Text unlockNoticeText;   // meta-unlock — 자기 PC 신규 언락 리스트
        private Button retryButton;
        private Button exitButton;

        private GameResult currentResult;

        // 자기 PC 의 신규 언락 캐시 (RunEnded 직후 UnlockTracker.OnNewUnlocks 발화 시 채워짐).
        // Show 시 한 번 표시 후 클리어. D5 — 각 클라가 자기 신규 언락만 표시.
        private readonly List<UnlockableId> pendingNewUnlocks = new List<UnlockableId>();

        private void Awake()
        {
            EnsureCanvasGroup();
            BuildUI();
            gameObject.SetActive(false);

            // 메타 언락 — 자기 PC UnlockTracker 의 OnNewUnlocks 구독.
            // gameObject 비활성 상태여도 컴포넌트 Awake 는 호출됨 → 구독 등록.
            // RunEnded 직후 UnlockTracker 가 평가/발화 → 이 핸들러가 pendingNewUnlocks 캐싱.
            var tracker = UnlockTracker.GetOrCreate();
            tracker.OnNewUnlocks -= HandleNewUnlocks;  // idempotent (씬 race 방어)
            tracker.OnNewUnlocks += HandleNewUnlocks;
        }

        private void OnDestroy()
        {
            if (UnlockTracker.Instance != null)
                UnlockTracker.Instance.OnNewUnlocks -= HandleNewUnlocks;
        }

        private void HandleNewUnlocks(List<UnlockableId> unlocks)
        {
            if (unlocks == null || unlocks.Count == 0) return;
            pendingNewUnlocks.AddRange(unlocks);
        }

        /// <summary>
        /// ResultManager → UIManager 경유로 호출.
        /// </summary>
        public void Show(GameResult result)
        {
            if (gameObject.activeSelf) return;  // ← 추가 (중복 호출 방어)
            currentResult = result;
            gameObject.SetActive(true);

            PopulateData(result);
            PlayShowAnimation();
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
                    gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        // ===== UI 구축 =====

        private void EnsureCanvasGroup()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void BuildUI()
        {
            // 반투명 배경
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            bg.raycastTarget = true;

            // RectTransform 풀스크린
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 타이틀 (상단)
            titleText = CreateTMP("ResultTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(600f, 80f),
                48, TextAlignmentOptions.Center);

            // 팀 통계 (중상단)
            statsText = CreateTMP("StatsText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -160f), new Vector2(500f, 120f),
                22, TextAlignmentOptions.Center);

            // 빌드 요약 (중앙)
            buildsText = CreateTMP("BuildsText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(700f, 250f),
                18, TextAlignmentOptions.TopLeft);

            // 보스 혼돈 스킬 (빌드 아래)
            bossChaosText = CreateTMP("BossChaosText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -130f), new Vector2(500f, 40f),
                20, TextAlignmentOptions.Center);

            // 신규 언락 (보스 혼돈 텍스트 아래) — meta-unlock §D8 토스트
            unlockNoticeText = CreateTMP("UnlockNoticeText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, -180f), new Vector2(600f, 120f),
                18, TextAlignmentOptions.Center);
            unlockNoticeText.color = new Color(1f, 0.85f, 0.3f);  // 금색 — 보상 강조

            // 버튼 영역 (하단)
            retryButton = CreateButton("RetryButton", "다시 하기",
                new Vector2(0.5f, 0f), new Vector2(-100f, 80f), new Vector2(180f, 50f));
            retryButton.onClick.AddListener(OnRetryClicked);

            exitButton = CreateButton("ExitButton", "나가기",
                new Vector2(0.5f, 0f), new Vector2(100f, 80f), new Vector2(180f, 50f));
            exitButton.onClick.AddListener(OnExitClicked);
        }

        // ===== 데이터 채우기 =====

        private void PopulateData(GameResult result)
        {
            // 타이틀
            if (titleText != null)
            {
                titleText.text = result.IsCleared ? "클리어!" : "실패...";
                titleText.color = result.IsCleared
                    ? new Color(0.3f, 1f, 0.5f) // 초록
                    : new Color(1f, 0.3f, 0.3f); // 빨강
            }

            // 통계 (팀 합계) — 인-런 통계의 ΣDamageDealt 도 표시 (B-1a)
            if (statsText != null)
            {
                int minutes = Mathf.FloorToInt(result.PlayTime / 60f);
                int seconds = Mathf.FloorToInt(result.PlayTime % 60f);

                float teamDamage = 0f;
                if (result.PlayerBuilds != null)
                {
                    foreach (var b in result.PlayerBuilds)
                        teamDamage += b.DamageDealt;
                }

                statsText.text = $"플레이 타임: {minutes:00}:{seconds:00}\n" +
                                 $"최종 레벨: {result.TeamLevel}\n" +
                                 $"총 처치 수: {result.TotalKills}    팀 총 데미지: {teamDamage:N0}\n" +
                                 $"총 사망 횟수: {result.TotalDeaths}";
            }

            // 빌드 요약
            if (buildsText != null)
                buildsText.text = FormatBuilds(result);

            // 보스 혼돈 스킬
            if (bossChaosText != null)
            {
                if (result.BossChaosTypeId > 0)
                {
                    string chaosName = GetChaosName((ChaosEffectType)result.BossChaosTypeId);
                    bossChaosText.text = $"보스 혼돈 스킬: {chaosName}";
                    bossChaosText.color = new Color(1f, 0.6f, 0.2f);
                }
                else
                {
                    bossChaosText.text = "";
                }
            }

            // 신규 언락 (자기 PC) — UnlockTracker.OnNewUnlocks 가 캐싱한 페이로드 표시.
            PopulateUnlockNotice();
        }

        private void PopulateUnlockNotice()
        {
            if (unlockNoticeText == null) return;
            if (pendingNewUnlocks.Count == 0)
            {
                unlockNoticeText.text = "";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🎉 신규 언락");
            for (int i = 0; i < pendingNewUnlocks.Count; i++)
            {
                string line = FormatUnlockLine(pendingNewUnlocks[i]);
                if (!string.IsNullOrEmpty(line)) sb.AppendLine("• " + line);
            }
            unlockNoticeText.text = sb.ToString().TrimEnd();

            // 한 런당 1회 표시 후 캐시 클리어 — 다음 게임에 다시 누적.
            pendingNewUnlocks.Clear();
        }

        private string FormatUnlockLine(UnlockableId u)
        {
            switch (u.type)
            {
                case UnlockableType.Skill:
                {
                    var db = LevelUpManager.Instance?.SkillDB;
                    var s = db?.GetSkillById(u.id);
                    string name = (s != null && !string.IsNullOrEmpty(s.skillName)) ? s.skillName : $"#{u.id}";
                    return $"스킬: {name}";
                }
                case UnlockableType.Weapon:
                {
                    var db = GameManager.Instance?.WeaponDB;
                    if (db == null || db.All == null || u.id < 0 || u.id >= db.All.Count)
                        return $"무기: #{u.id}";
                    var w = db.All[u.id];
                    string name = (w != null && !string.IsNullOrEmpty(w.displayName)) ? w.displayName : $"#{u.id}";
                    return $"무기: {name}";
                }
                case UnlockableType.Character:
                {
                    var db = GameManager.Instance?.CharacterDB;
                    if (db == null || db.characters == null) return $"캐릭터: #{u.id}";
                    for (int i = 0; i < db.characters.Length; i++)
                    {
                        var c = db.characters[i];
                        if (c != null && c.id == u.id)
                        {
                            string name = !string.IsNullOrEmpty(c.displayName) ? c.displayName : $"#{u.id}";
                            return $"캐릭터: {name}";
                        }
                    }
                    return $"캐릭터: #{u.id}";
                }
                case UnlockableType.RefreshCharge:
                {
                    var catalog = GameManager.Instance?.UnlockCatalog;
                    int amount = 1;
                    if (catalog != null && catalog.refreshChargeNodes != null
                        && u.id >= 0 && u.id < catalog.refreshChargeNodes.Count)
                    {
                        amount = catalog.refreshChargeNodes[u.id].amount;
                    }
                    return $"새로고침 +{amount}";
                }
                default:
                    return "";
            }
        }

        private string FormatBuilds(GameResult result)
        {
            if (result.PlayerBuilds == null || result.PlayerBuilds.Length == 0)
                return "빌드 정보 없음";

            var sb = new System.Text.StringBuilder();
            float playTime = Mathf.Max(1f, result.PlayTime); // DPS 0-div 방지

            foreach (var build in result.PlayerBuilds)
            {
                sb.Append($"[P{build.ActorNumber}] {build.PlayerName}");

                // TODO: CharacterData에서 캐릭터 이름 조회 (CharacterDatabase 연동 후)
                if (build.CharacterId >= 0)
                    sb.Append($" (캐릭터 {build.CharacterId})");
                sb.AppendLine();

                // 인-런 통계 (B-1a — run-statistics.md §7)
                float dps = build.DamageDealt / playTime;
                sb.Append($"  킬 {build.RunKills}  사망 {build.RunDeaths}  ");
                sb.Append($"가해 {build.DamageDealt:N0}  받음 {build.DamageTaken:N0}  ");
                sb.AppendLine($"DPS {dps:N0}");

                // 스킬 목록 + 스킬별 데미지 (텍스트 막대)
                if (build.SkillIds != null && build.SkillIds.Length > 0)
                {
                    // 가장 큰 데미지 (스케일 기준)
                    float maxDmg = 0f;
                    if (build.SkillDamageDealt != null)
                    {
                        for (int i = 0; i < build.SkillDamageDealt.Length; i++)
                            if (build.SkillDamageDealt[i] > maxDmg) maxDmg = build.SkillDamageDealt[i];
                    }
                    if (maxDmg < 1f) maxDmg = 1f; // div-zero 방지

                    for (int i = 0; i < build.SkillIds.Length; i++)
                    {
                        string skillName = GetSkillName(build.SkillIds[i]);
                        int level = i < build.SkillLevels.Length ? build.SkillLevels[i] : 1;
                        float dmg = (build.SkillDamageDealt != null && i < build.SkillDamageDealt.Length)
                            ? build.SkillDamageDealt[i] : 0f;
                        int kills = (build.SkillKillCounts != null && i < build.SkillKillCounts.Length)
                            ? build.SkillKillCounts[i] : 0;

                        // 텍스트 막대 — 데미지 비율 기준 (max 12 칸)
                        int barLen = Mathf.Clamp(Mathf.RoundToInt(dmg / maxDmg * 12f), 0, 12);
                        string bar = new string('█', barLen) + new string('░', 12 - barLen);

                        sb.AppendLine($"  {skillName} Lv.{level}  {bar}  {dmg:N0}  ({kills} kills)");
                    }
                }

                // 혼돈 스킬
                if (build.ChaosTypeIds != null && build.ChaosTypeIds.Length > 0)
                {
                    sb.Append("  혼돈: ");
                    for (int i = 0; i < build.ChaosTypeIds.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(GetChaosName((ChaosEffectType)build.ChaosTypeIds[i]));
                    }
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ===== DOTween 연출 =====

        private void PlayShowAnimation()
        {
            canvasGroup.alpha = 0f;

            // 타이틀: 위에서 떨어지며 등장
            if (titleText != null)
            {
                var titleRT = titleText.rectTransform;
                var originalPos = titleRT.anchoredPosition;
                titleRT.anchoredPosition = originalPos + new Vector2(0f, 80f);
                titleRT.DOAnchorPos(originalPos, animDuration).SetEase(Ease.OutBack);
            }

            // 전체 페이드인
            canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutCubic);
        }

        // ===== 버튼 핸들러 =====

        private void OnRetryClicked()
        {
            ResultManager.Instance?.OnRetry();
        }

        private void OnExitClicked()
        {
            ResultManager.Instance?.OnExit();
        }

        // ===== 헬퍼: 이름 조회 =====

        private string GetSkillName(int skillId)
        {
            // LevelUpManager.SkillDB getter 노출(Unit 2)로 SkillDatabase 접근 가능.
            var s = LevelUpManager.Instance?.SkillDB?.GetSkillById(skillId);
            return (s != null && !string.IsNullOrEmpty(s.skillName)) ? s.skillName : $"스킬#{skillId}";
        }

        private string GetChaosName(ChaosEffectType type)
        {
            return type switch
            {
                ChaosEffectType.GlassCannon => "유리대포",
                ChaosEffectType.ChainExplosion => "연쇄 폭발",
                ChaosEffectType.BerserkMode => "폭주 모드",
                ChaosEffectType.AccelEngine => "가속 엔진",
                ChaosEffectType.Unity => "단결",
                ChaosEffectType.Gambler => "도박꾼",
                _ => "없음"
            };
        }

        // ===== UI 생성 헬퍼 =====

        private TMP_Text CreateTMP(string objName, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateButton(string objName, string label,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.35f, 1f);

            // 버튼 텍스트
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(go.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return go.GetComponent<Button>();
        }
    }
}
