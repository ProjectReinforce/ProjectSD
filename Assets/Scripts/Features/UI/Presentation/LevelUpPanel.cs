using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Shared.Data;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 레벨업 스킬 선택 패널.
    /// UIManager가 Setup() / Hide() / UpdateTimer()를 호출.
    /// 카드 클릭 → LevelUpManager.SubmitChoice().
    ///
    /// Hierarchy:
    /// LevelUpPanel (CanvasGroup + 이 스크립트)
    /// ├─ Background (Image 반투명 검정, Stretch)
    /// ├─ Content (중앙)
    /// │   ├─ Title (TMP_Text)
    /// │   ├─ CardContainer (HorizontalLayoutGroup)
    /// │   │   ├─ SkillCard_0
    /// │   │   ├─ SkillCard_1
    /// │   │   └─ SkillCard_2
    /// │   └─ TimerBar (Image Filled)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LevelUpPanel : MonoBehaviour
    {
        [Header("카드")]
        [SerializeField] private SkillCardUI[] skillCards = new SkillCardUI[3];

        [Header("UI 요소")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image timerBarFill;

        [Header("새로고침 (일반 스킬 패널 전용)")]
        [Tooltip("기본 GameplayConfig.baseSkillRefreshCharges 회 + 혼돈 스킬로 +N 가산. 혼돈/StatBoost 패널에선 자동 비활성.")]
        [SerializeField] private UnityEngine.UI.Button refreshButton;
        [Tooltip("(선택) 잔여 횟수 표시. 비워두면 표시 안 함.")]
        [SerializeField] private TMP_Text refreshCountText;
        [Tooltip("R 키 홀드 동안 채워지는 진행도 이미지(Filled). 비워두면 시각 표시만 생략, 키 동작은 유지.")]
        [SerializeField] private Image refreshHoldFill;
        [Tooltip("R 키를 몇 초 누르고 있어야 새로고침이 발동하는지.")]
        [SerializeField] private float refreshHoldDuration = 0.5f;

        [Header("연출 설정")]
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private float scaleDuration = 0.4f;
        [SerializeField] private float cardDelay = 0.1f;
        [SerializeField] private float cardSlideDuration = 0.3f;

        private CanvasGroup canvasGroup;
        private Sequence showSequence;
        private Sequence hideSequence;
        private bool isShowing = false;
        private bool hasSelected = false;
        // 한 레벨업 내에서 새로고침 1회만 허용. Setup 진입 시 리셋, OnClickRefresh 시 true.
        // 새로고침 응답은 RefreshCards 경로로 분리되어 이 플래그를 안 건드림.
        private bool currentLevelRefreshConsumed = false;

        // ===== 키보드 네비게이션 상태 =====
        // -1: 아직 아무 카드도 focus 안 됨 (A / D 첫 입력 대기)
        private int focusedIndex = -1;
        private readonly List<int> activeIndices = new List<int>();
        private float refreshHoldTimer = 0f;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            // 초기 상태: 투명 + 입력 차단
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveAllListeners();
                refreshButton.onClick.AddListener(OnClickRefresh);
            }
        }

        private void OnDisable()
        {
            KillTweens();
            ResetKeyboardState();
        }

        private void Update()
        {
            // 표시·연출 완료·미선택 상태에서만 입력 받음.
            if (!isShowing || hasSelected || !canvasGroup.interactable) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // 좌/우 이동 키는 PlayerMovement 와 동일하게 A/D.
            if (kb.aKey.wasPressedThisFrame)
                MoveFocus(-1);
            else if (kb.dKey.wasPressedThisFrame)
                MoveFocus(+1);

            if (kb.spaceKey.wasPressedThisFrame)
                ConfirmFocus();

            HandleRefreshHold(kb);
        }

        // ===== 외부 호출용 =====

        /// <summary>
        /// UIManager.ShowLevelUp()에서 호출.
        /// SetActive(true) 이후에 호출됨.
        /// 혼돈 스킬일 때는 chaosRarity 를 받아 카드 3 장에 같은 등급 표시.
        /// </summary>
        public void Setup(SkillData[] choices, bool isChaos, Rarity chaosRarity = Rarity.Common)
        {
            hasSelected = false;
            currentLevelRefreshConsumed = false;
            ResetKeyboardState();

            if (titleText != null)
                titleText.text = isChaos ? $"혼돈 선택 · {chaosRarity}" : "스킬을 선택하세요";

            for (int i = 0; i < skillCards.Length; i++)
            {
                if (i < choices.Length && choices[i] != null)
                {
                    skillCards[i].gameObject.SetActive(true);
                    // Chaos 면 rolledRarity 주입, 일반 스킬이면 기본값(Common).
                    skillCards[i].Setup(choices[i], OnCardClicked, chaosRarity);
                }
                else
                {
                    skillCards[i].gameObject.SetActive(false);
                }
            }

            RebuildActiveIndices();

            if (timerBarFill != null)
            {
                timerBarFill.fillAmount = 1f;
                timerBarFill.color = Color.white;
            }

            // 새로고침 버튼: 일반 스킬 패널만 활성. 혼돈은 비활성.
            UpdateRefreshButton(skillPanelActive: !isChaos);

            PlayShowAnimation();
        }

        /// <summary>
        /// StatBoost 선택지 패널 구성. 기존 SkillCardUI 카드를 재사용하되 boost 모드로 전환.
        /// rolledRarity 는 카드 3 장이 공유 — 각 SO 의 valueByRarity[rolled] 를 표시.
        /// UIManager.ShowLevelUpStatBoost()에서 호출.
        /// </summary>
        public void SetupStatBoost(StatBoostData[] choices, Rarity rolledRarity)
        {
            hasSelected = false;
            currentLevelRefreshConsumed = false;
            ResetKeyboardState();

            if (titleText != null)
                titleText.text = $"능력치 선택 · {rolledRarity}";

            for (int i = 0; i < skillCards.Length; i++)
            {
                if (i < choices.Length && choices[i] != null)
                {
                    skillCards[i].gameObject.SetActive(true);
                    skillCards[i].SetupAsStatBoost(choices[i], rolledRarity, OnBoostCardClicked);
                }
                else
                {
                    skillCards[i].gameObject.SetActive(false);
                }
            }

            RebuildActiveIndices();

            if (timerBarFill != null)
            {
                timerBarFill.fillAmount = 1f;
                timerBarFill.color = Color.white;
            }

            // StatBoost 패널은 새로고침 비활성.
            UpdateRefreshButton(skillPanelActive: false);

            PlayShowAnimation();
        }

        // ===== 새로고침 =====

        private void UpdateRefreshButton(bool skillPanelActive)
        {
            if (refreshButton == null) return;

            // 스킬 패널이 아니면 숨김.
            if (!skillPanelActive)
            {
                refreshButton.gameObject.SetActive(false);
                if (refreshCountText != null) refreshCountText.gameObject.SetActive(false);
                return;
            }

            refreshButton.gameObject.SetActive(true);

            int remaining = 0;
            var mgr = SwDreams.Features.Progression.Adapter.LevelUpManager.Instance;
            if (mgr != null)
                remaining = mgr.LocalPlayerRefreshRemaining;

            // 한 레벨업 내 1회 가드 + 잔여 0 가드 — 둘 다 통과해야 활성.
            refreshButton.interactable = remaining > 0 && !currentLevelRefreshConsumed;

            if (refreshCountText != null)
            {
                refreshCountText.gameObject.SetActive(true);
                refreshCountText.text = remaining.ToString();
            }
        }

        private void OnClickRefresh()
        {
            var mgr = SwDreams.Features.Progression.Adapter.LevelUpManager.Instance;
            if (mgr == null) return;
            if (currentLevelRefreshConsumed) return;

            currentLevelRefreshConsumed = true;
            mgr.RequestRefresh();
            // 즉시 비활성 — RefreshCards 응답이 와도 consumed=true 라 다시 활성 안 됨.
            if (refreshButton != null) refreshButton.interactable = false;
        }

        /// <summary>
        /// 새로고침 응답으로 카드만 교체. Setup 과 달리 currentLevelRefreshConsumed 를 안 건드려서
        /// 한 레벨업 1회 가드가 유지된다. 카드 fade out → 데이터 교체 → 슬라이드+fade in 애니메이션.
        /// </summary>
        public void RefreshCards(SkillData[] choices, Rarity chaosRarity = Rarity.Common)
        {
            KillTweens();
            canvasGroup.interactable = false;
            ResetKeyboardState();

            var refreshSeq = DOTween.Sequence();

            // 1) 기존 카드 fade out (절반 시간)
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (!skillCards[i].gameObject.activeSelf) continue;
                int idx = i;
                refreshSeq.Join(skillCards[idx].DOFadeCard(0f, cardSlideDuration * 0.5f));
            }

            // 2) 데이터 교체 + 새 카드 시작 위치(아래) + alpha=0 으로 reset
            refreshSeq.AppendCallback(() =>
            {
                for (int i = 0; i < skillCards.Length; i++)
                {
                    if (i < choices.Length && choices[i] != null)
                    {
                        skillCards[i].gameObject.SetActive(true);
                        skillCards[i].Setup(choices[i], OnCardClicked, chaosRarity);
                        var rt = skillCards[i].GetComponent<RectTransform>();
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -100f);
                        skillCards[i].SetAlpha(0f);
                    }
                    else
                    {
                        skillCards[i].gameObject.SetActive(false);
                    }
                }
                RebuildActiveIndices();
                UpdateRefreshButton(skillPanelActive: true);
            });

            // 3) 새 카드 순차 슬라이드 + fade in (PlayShowAnimation 의 카드 부분과 동일)
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (i >= choices.Length || choices[i] == null) continue;
                int idx = i;
                var rt = skillCards[idx].GetComponent<RectTransform>();
                refreshSeq.Append(rt.DOAnchorPosY(0f, cardSlideDuration).SetEase(Ease.OutBack));
                refreshSeq.Join(skillCards[idx].DOFadeCard(1f, cardSlideDuration));
                if (i < skillCards.Length - 1)
                    refreshSeq.AppendInterval(cardDelay);
            }

            // 4) 입력 재활성화
            refreshSeq.OnComplete(() => canvasGroup.interactable = true);
            refreshSeq.SetUpdate(true);
            showSequence = refreshSeq;
        }

        /// <summary>
        /// UIManager.HideLevelUp()에서 호출.
        /// </summary>
        public void Hide()
        {
            ResetKeyboardState();

            if (!isShowing)
            {
                // 연출 중이 아니면 바로 비활성화
                gameObject.SetActive(false);
                return;
            }
            PlayHideAnimation();
        }

        /// <summary>
        /// UIManager.UpdateLevelUpTimer()에서 호출.
        /// </summary>
        public void UpdateTimer(float remaining, float total)
        {
            if (timerBarFill == null || total <= 0f) return;

            timerBarFill.fillAmount = Mathf.Clamp01(remaining / total);

            if (remaining <= 5f)
                timerBarFill.color = Color.Lerp(Color.red, Color.yellow, remaining / 5f);
            else
                timerBarFill.color = Color.white;
        }

        // ===== 카드 클릭 =====

        private void OnCardClicked(SkillData selectedSkill)
        {
            if (hasSelected) return;
            hasSelected = true;

            Debug.Log($"[LevelUpPanel] 카드 선택: {selectedSkill.skillName}");

            for (int i = 0; i < skillCards.Length; i++)
            {
                if (!skillCards[i].gameObject.activeSelf) continue;

                if (skillCards[i].CurrentSkillData == selectedSkill)
                    skillCards[i].PlaySelectedAnimation();
                else
                    skillCards[i].PlayDimAnimation();
            }

            if (LevelUpManager.Instance != null)
                LevelUpManager.Instance.SubmitChoice(selectedSkill.skillId);

            canvasGroup.interactable = false;
        }

        private void OnBoostCardClicked(StatBoostData selectedBoost)
        {
            if (hasSelected) return;
            hasSelected = true;

            Debug.Log($"[LevelUpPanel] StatBoost 카드 선택: {selectedBoost.displayName}");

            for (int i = 0; i < skillCards.Length; i++)
            {
                if (!skillCards[i].gameObject.activeSelf) continue;

                if (skillCards[i].CurrentStatBoostData == selectedBoost)
                    skillCards[i].PlaySelectedAnimation();
                else
                    skillCards[i].PlayDimAnimation();
            }

            if (LevelUpManager.Instance != null)
                LevelUpManager.Instance.SubmitBoostChoice(selectedBoost.boostId);

            canvasGroup.interactable = false;
        }

        // ===== DOTween 연출 =====

        private void PlayShowAnimation()
        {
            KillTweens();
            isShowing = true;

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
            transform.localScale = Vector3.one * 0.8f;

            // 카드 초기 위치 (아래로 밀어놓기)
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (!skillCards[i].gameObject.activeSelf) continue;
                var rt = skillCards[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -100f);
                skillCards[i].SetAlpha(0f);
            }

            showSequence = DOTween.Sequence();

            // 1) 배경 페이드 + 스케일
            showSequence.Append(canvasGroup.DOFade(1f, fadeDuration));
            showSequence.Join(transform.DOScale(1f, scaleDuration).SetEase(Ease.OutBack));

            // 2) 카드 순차 등장
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (!skillCards[i].gameObject.activeSelf) continue;

                int idx = i;
                var rt = skillCards[idx].GetComponent<RectTransform>();

                showSequence.Append(
                    rt.DOAnchorPosY(0f, cardSlideDuration).SetEase(Ease.OutBack)
                );
                showSequence.Join(
                    skillCards[idx].DOFadeCard(1f, cardSlideDuration)
                );

                if (i < skillCards.Length - 1)
                    showSequence.AppendInterval(cardDelay);
            }

            // 3) 입력 활성화
            showSequence.OnComplete(() => canvasGroup.interactable = true);
            showSequence.SetUpdate(true); // timeScale 0에서도 동작
        }

        private void PlayHideAnimation()
        {
            KillTweens();
            canvasGroup.interactable = false;

            hideSequence = DOTween.Sequence();
            hideSequence.Append(canvasGroup.DOFade(0f, fadeDuration * 0.5f));
            hideSequence.Join(transform.DOScale(0.9f, fadeDuration * 0.5f).SetEase(Ease.InBack));

            hideSequence.OnComplete(() =>
            {
                isShowing = false;
                canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
            });

            hideSequence.SetUpdate(true);
        }

        private void KillTweens()
        {
            showSequence?.Kill();
            hideSequence?.Kill();
            showSequence = null;
            hideSequence = null;
        }

        // ===== 키보드 네비게이션 =====

        private void ResetKeyboardState()
        {
            focusedIndex = -1;
            refreshHoldTimer = 0f;
            UpdateRefreshHoldFill(0f);

            // 활성 카드들 focus 시각 해제 — Setup 의 transform.localScale = 1 이 시각도 자동 리셋하지만
            // 명시적 호출로 RefreshCards 같은 부분 갱신 경로에서도 안전.
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (skillCards[i] != null && skillCards[i].gameObject.activeSelf)
                    skillCards[i].SetFocused(false);
            }
        }

        private void RebuildActiveIndices()
        {
            activeIndices.Clear();
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (skillCards[i] != null && skillCards[i].gameObject.activeSelf)
                    activeIndices.Add(i);
            }
        }

        private void MoveFocus(int dir)
        {
            if (activeIndices.Count == 0) return;

            if (focusedIndex < 0)
            {
                // 첫 입력: A 면 가장 왼쪽(첫 활성), D 면 가장 오른쪽(마지막 활성).
                focusedIndex = (dir < 0)
                    ? activeIndices[0]
                    : activeIndices[activeIndices.Count - 1];
            }
            else
            {
                int curPos = activeIndices.IndexOf(focusedIndex);
                int newPos = Mathf.Clamp(curPos + dir, 0, activeIndices.Count - 1);
                focusedIndex = activeIndices[newPos];
            }

            ApplyFocusVisual();
        }

        private void ApplyFocusVisual()
        {
            for (int i = 0; i < skillCards.Length; i++)
            {
                if (skillCards[i] == null || !skillCards[i].gameObject.activeSelf) continue;
                skillCards[i].SetFocused(i == focusedIndex);
            }
        }

        private void ConfirmFocus()
        {
            if (focusedIndex < 0 || focusedIndex >= skillCards.Length) return;
            var card = skillCards[focusedIndex];
            if (card == null || !card.gameObject.activeSelf) return;

            // Skill 모드 우선, 아니면 StatBoost 모드 — SkillCardUI.OnClick 과 동일 규약.
            if (card.CurrentSkillData != null)
                OnCardClicked(card.CurrentSkillData);
            else if (card.CurrentStatBoostData != null)
                OnBoostCardClicked(card.CurrentStatBoostData);
        }

        // ===== R 홀드 새로고침 =====

        private bool CanRefreshNow()
        {
            return refreshButton != null
                && refreshButton.gameObject.activeSelf
                && refreshButton.interactable;
        }

        private void HandleRefreshHold(Keyboard kb)
        {
            bool holdingR = kb.rKey.isPressed && CanRefreshNow();

            if (holdingR)
            {
                refreshHoldTimer += Time.unscaledDeltaTime;
                UpdateRefreshHoldFill(refreshHoldTimer / Mathf.Max(0.01f, refreshHoldDuration));

                if (refreshHoldTimer >= refreshHoldDuration)
                {
                    refreshHoldTimer = 0f;
                    UpdateRefreshHoldFill(0f);
                    OnClickRefresh();
                }
            }
            else if (refreshHoldTimer > 0f)
            {
                refreshHoldTimer = 0f;
                UpdateRefreshHoldFill(0f);
            }
        }

        private void UpdateRefreshHoldFill(float t)
        {
            if (refreshHoldFill == null) return;
            refreshHoldFill.fillAmount = Mathf.Clamp01(t);
        }
    }
}