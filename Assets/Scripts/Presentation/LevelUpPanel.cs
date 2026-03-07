using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Data;
using SwDreams.Adapter.Manager;

namespace SwDreams.Presentation
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

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            // 초기 상태: 투명 + 입력 차단
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void OnDisable()
        {
            KillTweens();
        }

        // ===== 외부 호출용 =====

        /// <summary>
        /// UIManager.ShowLevelUp()에서 호출.
        /// SetActive(true) 이후에 호출됨.
        /// </summary>
        public void Setup(SkillData[] choices, bool isChaos)
        {
            hasSelected = false;

            if (titleText != null)
                titleText.text = isChaos ? "혼돈 스킬을 선택하세요" : "스킬을 선택하세요";

            for (int i = 0; i < skillCards.Length; i++)
            {
                if (i < choices.Length && choices[i] != null)
                {
                    skillCards[i].gameObject.SetActive(true);
                    skillCards[i].Setup(choices[i], OnCardClicked);
                }
                else
                {
                    skillCards[i].gameObject.SetActive(false);
                }
            }

            if (timerBarFill != null)
            {
                timerBarFill.fillAmount = 1f;
                timerBarFill.color = Color.white;
            }

            PlayShowAnimation();
        }

        /// <summary>
        /// UIManager.HideLevelUp()에서 호출.
        /// </summary>
        public void Hide()
        {
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
    }
}