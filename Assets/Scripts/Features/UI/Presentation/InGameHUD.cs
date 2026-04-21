using System.Collections.Generic;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Skill.Adapter.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Data;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 인게임 HUD 총괄.
    /// 이벤트 구독 방식으로 UI 갱신 (Update 폴링 최소화).
    ///
    /// 표시 항목:
    /// - 플레이어 체력 바
    /// - 경험치 바 + 레벨 표시
    /// - 게임 타이머 (10분 카운트다운)
    /// - 스킬 슬롯 아이콘 (최대 6개)
    /// - 팀원 상태 (미니 체력 바)
    /// - 보유 혼돈 스킬 아이콘
    ///
    /// 셋업:
    /// GameScene Canvas 하위에 "InGameHUD" 오브젝트 생성.
    /// 각 UI 요소를 자식으로 배치 후 Inspector에서 연결.
    /// TMP 텍스트 기준.
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        [Header("체력")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_Text healthText;

        [Header("경험치")]
        [SerializeField] private Slider expSlider;
        [SerializeField] private TMP_Text levelText;

        [Header("타이머")]
        [SerializeField] private TMP_Text timerText;

        [Header("스킬 슬롯")]
        [SerializeField] private Transform skillSlotContainer;
        [SerializeField] private GameObject skillSlotPrefab;
        // skillSlotPrefab 내부: Image (아이콘) + TMP_Text (레벨)

        [Header("팀원 상태")]
        [SerializeField] private Transform teammateContainer;
        [SerializeField] private GameObject teammatePrefab;
        // teammatePrefab 내부: TMP_Text (이름) + Slider (HP 바)

        [Header("혼돈 스킬")]
        [SerializeField] private Transform chaosIconContainer;
        [SerializeField] private GameObject chaosIconPrefab;
        // chaosIconPrefab: Image (아이콘)

        // 로컬 플레이어 참조
        private IDamageable localDamageable;
        private SkillManager localSkillManager;
        private ChaosSkillManager localChaosManager;
        private bool isInitialized = false;

        // 스킬 슬롯 UI 캐시
        private List<SkillSlotEntry> skillSlotEntries = new List<SkillSlotEntry>();

        // 팀원 UI 캐시
        private Dictionary<int, TeammateEntry> teammateEntries = new Dictionary<int, TeammateEntry>();

        // ===== 혼돈 스킬 아이콘 =====

        private List<GameObject> chaosIconObjects = new List<GameObject>();


        private void Update()
        {
            if (!isInitialized)
            {
                TryInitialize();
                return;
            }

            UpdateTimer();
            UpdateTeammates();
            RefreshChaosIcons();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ===== 초기화 =====

        private void TryInitialize()
        {
            // 로컬 플레이어 찾기
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    localDamageable = p.GetComponent<IDamageable>();
                    localSkillManager = p.GetComponentInChildren<SkillManager>();
                    localChaosManager = p.GetComponentInChildren<ChaosSkillManager>();

                    SubscribeEvents();
                    InitializeUI();
                    isInitialized = true;
                    return;
                }
            }
        }

        private void SubscribeEvents()
        {
            if (localDamageable != null)
                localDamageable.OnHealthChanged += UpdateHealth;

            if (localSkillManager != null)
            {
                localSkillManager.OnSkillAdded += OnSkillAdded;
                localSkillManager.OnSkillLeveledUp += OnSkillLeveledUp;
                localSkillManager.OnSkillRemoved += OnSkillRemoved;
                localSkillManager.OnEvolution += OnEvolution;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.OnExpChanged += UpdateExp;
        }

        private void UnsubscribeEvents()
        {
            if (localDamageable != null)
                localDamageable.OnHealthChanged -= UpdateHealth;

            if (localSkillManager != null)
            {
                localSkillManager.OnSkillAdded -= OnSkillAdded;
                localSkillManager.OnSkillLeveledUp -= OnSkillLeveledUp;
                localSkillManager.OnSkillRemoved -= OnSkillRemoved;
                localSkillManager.OnEvolution -= OnEvolution;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.OnExpChanged -= UpdateExp;
        }

        private void InitializeUI()
        {
            // 초기 HP
            if (localDamageable != null)
                UpdateHealth(localDamageable.CurrentHP, localDamageable.MaxHP);

            // 초기 EXP — 프리팹 Inspector 기본값으로 표시되는 현상 방지
            if (GameManager.Instance != null)
                UpdateExp(GameManager.Instance.TeamExp, GameManager.Instance.TeamRequiredExp);

            // 초기 스킬 슬롯
            RefreshAllSkillSlots();
        }

        // ===== 체력 =====

        private void UpdateHealth(int current, int max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }

            if (healthText != null)
                healthText.text = $"{current}/{max}";
        }

        // ===== 경험치 =====

        private void UpdateExp(int current, int required)
        {
            if (expSlider != null)
            {
                expSlider.maxValue = required;
                expSlider.value = current;
            }

            if (levelText != null && GameManager.Instance != null)
                levelText.text = $"Lv.{GameManager.Instance.TeamLevel}";
        }

        // ===== 타이머 =====

        private void UpdateTimer()
        {
            if (timerText == null) return;
            if (GameManager.Instance == null) return;

            float gameTime = GameManager.Instance.GameTime;
            float totalTime = GameManager.Instance.Config?.bossSpawnTime ?? 600f;
            float remaining = Mathf.Max(0, totalTime - gameTime);

            int min = Mathf.FloorToInt(remaining / 60f);
            int sec = Mathf.FloorToInt(remaining % 60f);

            timerText.text = $"{min:00}:{sec:00}";

            // 보스전이면 경과 시간 표시
            if (GameManager.Instance.CurrentState == GameManager.GameState.BossFight)
            {
                float bossTime = Mathf.Max(0, gameTime - totalTime);
                int bMin = Mathf.FloorToInt(bossTime / 60f);
                int bSec = Mathf.FloorToInt(bossTime % 60f);
                timerText.text = $"BOSS {bMin:00}:{bSec:00}";
            }
        }

        // ===== 스킬 슬롯 =====

        private void OnSkillAdded(SwDreams.Features.Skill.Adapter.Skill skill)
        {
            RefreshAllSkillSlots();
        }

        private void OnSkillLeveledUp(SwDreams.Features.Skill.Adapter.Skill skill)
        {
            RefreshAllSkillSlots();
        }

        private void OnSkillRemoved(int skillId)
        {
            RefreshAllSkillSlots();
        }

        private void OnEvolution(SkillData evolvedData)
        {
            RefreshAllSkillSlots();
        }

        private void RefreshAllSkillSlots()
        {
            if (skillSlotContainer == null || skillSlotPrefab == null) return;
            if (localSkillManager == null) return;

            // 기존 슬롯 정리
            foreach (var entry in skillSlotEntries)
                Destroy(entry.obj);
            skillSlotEntries.Clear();

            // 현재 스킬로 재생성
            var skills = localSkillManager.EquippedSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                GameObject slotObj = Instantiate(skillSlotPrefab, skillSlotContainer);

                var icon = slotObj.GetComponentInChildren<Image>();
                var lvlText = slotObj.GetComponentInChildren<TMP_Text>();

                if (icon != null && skill.Data.icon != null)
                    icon.sprite = skill.Data.icon;

                if (lvlText != null)
                    lvlText.text = $"{skill.Level}";

                skillSlotEntries.Add(new SkillSlotEntry
                {
                    obj = slotObj,
                    skillId = skill.Data.skillId
                });
            }
        }

        /// <summary>
        /// 혼돈 스킬 아이콘 갱신. Update에서 주기적 호출 (이벤트 없으므로).
        /// </summary>
        private void RefreshChaosIcons()
        {
            if (chaosIconContainer == null || chaosIconPrefab == null) return;
            if (localChaosManager == null) return;

            var effects = localChaosManager.ActiveEffects;

            // 개수 변화 없으면 스킵
            if (effects.Count == chaosIconObjects.Count) return;

            // 재생성
            foreach (var obj in chaosIconObjects)
                Destroy(obj);
            chaosIconObjects.Clear();

            foreach (var effectType in effects)
            {
                GameObject iconObj = Instantiate(chaosIconPrefab, chaosIconContainer);
                var text = iconObj.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = effectType.ToString().Substring(0, 2);
                chaosIconObjects.Add(iconObj);
            }
        }

        // ===== 팀원 상태 =====

        private void UpdateTeammates()
        {
            if (teammateContainer == null || teammatePrefab == null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");

            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv == null || pv.IsMine) continue; // 자신 제외

                int viewId = pv.ViewID;
                var damageable = p.GetComponent<IDamageable>();
                if (damageable == null) continue;

                if (!teammateEntries.ContainsKey(viewId))
                {
                    // 새 팀원 UI 생성
                    GameObject tmObj = Instantiate(teammatePrefab, teammateContainer);
                    var entry = new TeammateEntry
                    {
                        obj = tmObj,
                        nameText = tmObj.GetComponentInChildren<TMP_Text>(),
                        hpSlider = tmObj.GetComponentInChildren<Slider>()
                    };

                    if (entry.nameText != null && pv.Owner != null)
                        entry.nameText.text = pv.Owner.NickName;

                    teammateEntries[viewId] = entry;
                }

                // HP 갱신
                var tm = teammateEntries[viewId];
                if (tm.hpSlider != null)
                {
                    tm.hpSlider.maxValue = damageable.MaxHP;
                    tm.hpSlider.value = damageable.CurrentHP;
                }
            }
        }

        // ===== 내부 구조체 =====

        private struct SkillSlotEntry
        {
            public GameObject obj;
            public int skillId;
        }

        private class TeammateEntry
        {
            public GameObject obj;
            public TMP_Text nameText;
            public Slider hpSlider;
        }
    }
}