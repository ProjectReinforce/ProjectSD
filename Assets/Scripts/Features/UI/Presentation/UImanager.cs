using UnityEngine;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Skill.Adapter.Data;
using DG.Tweening;
using SwDreams.Shared.Domain;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// GameScene UI 관리. 패널 열기/닫기만 담당.
    /// LevelUpManager가 직접 UIManager.Instance.ShowLevelUp()을 호출.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("패널 참조")]
        [SerializeField] private LevelUpPanel levelUpPanel;
        [SerializeField] private ResultPanelUI resultPanel;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);

            // 패널 비활성화
            if (levelUpPanel != null)
                levelUpPanel.gameObject.SetActive(false);
            if (resultPanel != null)
                resultPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ===== 결과 화면 =====

        public void ShowResult(GameResult result)
        {
            if (resultPanel == null)
            {
                Debug.LogError("[UIManager] ResultPanelUI 참조 없음!");
                return;
            }

            Debug.Log("[UIManager] ShowResult 호출");
            resultPanel.Show(result);
        }

        public void HideResult()
        {
            if (resultPanel != null)
                resultPanel.Hide();
        }

        // ===== LevelUpManager에서 직접 호출 =====

        public void ShowLevelUp(
            SwDreams.Features.Skill.Adapter.Data.SkillData[] choices,
            bool isChaos,
            SwDreams.Shared.Domain.ValueObjects.Rarity chaosRarity = SwDreams.Shared.Domain.ValueObjects.Rarity.Common)
        {
            if (levelUpPanel == null)
            {
                Debug.LogError("[UIManager] LevelUpPanel 참조 없음!");
                return;
            }

            Debug.Log($"[UIManager] ShowLevelUp 호출 (isChaos={isChaos}, rarity={chaosRarity})");
            levelUpPanel.gameObject.SetActive(true);
            levelUpPanel.Setup(choices, isChaos, chaosRarity);
        }

        /// <summary>
        /// 새로고침 응답 — 이미 표시된 패널의 카드만 교체. currentLevelRefreshConsumed 가드 유지.
        /// </summary>
        public void RefreshLevelUpCards(
            SwDreams.Features.Skill.Adapter.Data.SkillData[] choices)
        {
            if (levelUpPanel == null) return;
            levelUpPanel.RefreshCards(choices);
        }

        /// <summary>
        /// StatBoost 선택지 패널 오픈. 스킬 풀 고갈(만렙) 시 레벨업 또는 퀘스트 보상에서 호출.
        /// rolledRarity 는 카드 3 장이 공유하는 등급 — 각 SO 에서 등급별 value 를 꺼내 표시.
        /// </summary>
        public void ShowLevelUpStatBoost(
            SwDreams.Features.StatBoost.Adapter.Data.StatBoostData[] choices,
            SwDreams.Shared.Domain.ValueObjects.Rarity rolledRarity)
        {
            if (levelUpPanel == null)
            {
                Debug.LogError("[UIManager] LevelUpPanel 참조 없음!");
                return;
            }

            Debug.Log($"[UIManager] ShowLevelUpStatBoost 호출 ({rolledRarity})");
            levelUpPanel.gameObject.SetActive(true);
            levelUpPanel.SetupStatBoost(choices, rolledRarity);
        }

        public void HideLevelUp()
        {
            if (levelUpPanel != null)
                levelUpPanel.Hide();
        }

        public void UpdateLevelUpTimer(float remaining, float total)
        {
            if (levelUpPanel != null && levelUpPanel.gameObject.activeSelf)
                levelUpPanel.UpdateTimer(remaining, total);
        }
    }
}