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

        public void ShowLevelUp(SwDreams.Features.Skill.Adapter.Data.SkillData[] choices, bool isChaos)
        {
            if (levelUpPanel == null)
            {
                Debug.LogError("[UIManager] LevelUpPanel 참조 없음!");
                return;
            }

            Debug.Log("[UIManager] ShowLevelUp 호출");
            levelUpPanel.gameObject.SetActive(true);
            levelUpPanel.Setup(choices, isChaos);
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