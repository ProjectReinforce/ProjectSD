using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Data;
using SwDreams.Adapter.Skill;

namespace SwDreams.Presentation
{
    /// <summary>
    /// 레벨업 선택지 개별 카드.
    ///
    /// Hierarchy:
    /// SkillCard (Button + Image(배경) + CanvasGroup + SkillCardUI)
    /// ├─ Icon (Image)
    /// ├─ NameText (TMP_Text)
    /// ├─ DescText (TMP_Text)
    /// ├─ LevelBadge (Image)
    /// │   └─ LevelText (TMP_Text)
    /// └─ TypeBadge (Image)
    ///     └─ TypeText (TMP_Text)
    /// </summary>
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SkillCardUI : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image typeBadge;

        [Header("타입별 색상")]
        [SerializeField] private Color activeColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color passiveColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color chaosColor = new Color(0.8f, 0.2f, 0.8f, 1f);
        [SerializeField] private Color newBadgeColor = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color levelUpBadgeColor = new Color(0.4f, 0.7f, 1f, 1f);

        private Button button;
        private CanvasGroup canvasGroup;
        private SkillData currentSkillData;
        private Action<SkillData> onClickCallback;
        private Color defaultCardColor;

        public SkillData CurrentSkillData => currentSkillData;

        private void Awake()
        {
            button = GetComponent<Button>();
            canvasGroup = GetComponent<CanvasGroup>();
            button.onClick.AddListener(OnClick);

            if (cardBackground != null)
                defaultCardColor = cardBackground.color;
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(OnClick);
        }

        public void Setup(SkillData skillData, Action<SkillData> onClick)
        {
            DOTween.Kill(transform);
            DOTween.Kill(canvasGroup);
            if (cardBackground != null)
                DOTween.Kill(cardBackground);

            currentSkillData = skillData;
            onClickCallback = onClick;

            currentSkillData = skillData;
            onClickCallback = onClick;

            if (nameText != null)
                nameText.text = skillData.skillName;

            if (descText != null)
                descText.text = GetDescription(skillData);

            if (iconImage != null && skillData.icon != null)
                iconImage.sprite = skillData.icon;

            SetupLevelBadge(skillData);
            SetupTypeBadge(skillData);

            canvasGroup.alpha = 1f;
            button.interactable = true;
            transform.localScale = Vector3.one;

            if (cardBackground != null)
                cardBackground.color = defaultCardColor;
        }

        private void SetupLevelBadge(SkillData skillData)
        {
            if (levelText == null) return;

            SkillManager sm = FindLocalSkillManager();

            // 진화 스킬인지 확인
            if (sm != null)
            {
                var evos = sm.GetPendingEvolutions();
                for (int i = 0; i < evos.Count; i++)
                {
                    if (evos[i].evolvedSkillData.skillId == skillData.skillId)
                    {
                        levelText.text = "★ 진화";
                        levelText.color = new Color(1f, 0.5f, 0f, 1f); // 주황
                        return;
                    }
                }
            }

            if (sm != null && sm.HasSkill(skillData.skillId))
            {
                // SkillManager.GetSkill()은 Adapter.Skill.Skill 반환
                var existing = sm.GetSkill(skillData.skillId);
                int currentLv = existing.Level;
                levelText.text = $"Lv.{currentLv} → Lv.{currentLv + 1}";
                levelText.color = levelUpBadgeColor;
            }
            else
            {
                levelText.text = "NEW";
                levelText.color = newBadgeColor;
            }
        }

        private void SetupTypeBadge(SkillData skillData)
        {
            Color badgeColor;
            string typeStr;

            switch (skillData.skillType)
            {
                case SkillType.Active:
                    badgeColor = activeColor;
                    typeStr = "액티브";
                    break;
                case SkillType.Passive:
                    badgeColor = passiveColor;
                    typeStr = "패시브";
                    break;
                case SkillType.Chaos:
                    badgeColor = chaosColor;
                    typeStr = "혼돈";
                    break;
                default:
                    badgeColor = Color.gray;
                    typeStr = "???";
                    break;
            }

            if (typeBadge != null)
                typeBadge.color = badgeColor;
            if (typeText != null)
                typeText.text = typeStr;
        }

        private string GetDescription(SkillData skillData)
        {
            if (!string.IsNullOrEmpty(skillData.description))
                return skillData.description;

            switch (skillData.skillType)
            {
                case SkillType.Active:
                    return $"자동 발동 공격 스킬\n쿨다운: {skillData.GetCooldownForLevel(1):F1}초";
                case SkillType.Passive:
                    return "영구 능력치 강화";
                case SkillType.Chaos:
                    return "게임 규칙을 변경합니다";
                default:
                    return "";
            }
        }

        private void OnClick()
        {
            onClickCallback?.Invoke(currentSkillData);
        }

        // ===== 연출 (LevelUpPanel에서 호출) =====

        public void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
        }

        public Tween DOFadeCard(float endValue, float duration)
        {
            return canvasGroup.DOFade(endValue, duration);
        }

        public void PlaySelectedAnimation()
        {
            transform.DOScale(1.08f, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);

            if (cardBackground != null)
                cardBackground.DOColor(new Color(1f, 0.9f, 0.3f, 1f), 0.2f).SetUpdate(true);
        }

        public void PlayDimAnimation()
        {
            button.interactable = false;
            transform.DOScale(0.95f, 0.2f).SetEase(Ease.InQuad).SetUpdate(true);
            canvasGroup.DOFade(0.4f, 0.2f).SetUpdate(true);
        }

        // ===== 유틸 =====

        private SkillManager FindLocalSkillManager()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var player in players)
            {
                var pv = player.GetComponent<Photon.Pun.PhotonView>();
                if (pv != null && pv.IsMine)
                    return player.GetComponentInChildren<SkillManager>();
            }
            return null;
        }
    }
}