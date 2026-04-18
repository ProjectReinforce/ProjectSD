using UnityEngine;
using SwDreams.Features.UI.Presentation;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using SwDreams.Shared.Managers;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 간이 디버그 오버레이.
    /// 화면 좌상단에 게임 상태를 실시간 표시.
    ///
    /// 표시 항목:
    /// - 게임 상태 / 시간
    /// - 팀 레벨 / 경험치
    /// - HP
    /// - 보유 스킬 목록 + 레벨
    /// - 네트워크 정보 (호스트 여부, 인원)
    ///
    /// 조작: Tab 토글 (기본 켜짐)
    ///
    /// 셋업:
    /// GameScene에 빈 GameObject → DebugOverlay 부착.
    /// Canvas + TMP_Text를 자동 생성하므로 수동 설정 불필요.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private float updateInterval = 0.2f;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private TMP_FontAsset fontAsset;

        // 자동 생성 UI 참조
        private Canvas canvas;
        private TMP_Text displayText;
        private Image backgroundImage;

        // 플레이어 참조 (로컬)
        private SkillManager localSkillManager;
        private IDamageable localDamageable;
        private Transform localPlayer;

        // 상태
        private bool isVisible = true;
        private float updateTimer;

        private void Awake()
        {
            CreateUI();
        }

        private void Update()
        {
            // Tab 토글
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                isVisible = !isVisible;
                canvas.gameObject.SetActive(isVisible);
            }

            if (!isVisible) return;

            updateTimer += Time.unscaledDeltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                CacheLocalPlayer();
                RefreshDisplay();
            }
        }

        /// <summary>
        /// Canvas + Background + TMP_Text 자동 생성.
        /// </summary>
        private void CreateUI()
        {
            // Canvas
            GameObject canvasObj = new GameObject("DebugOverlay_Canvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 최상위

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Background Panel
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0, 1);
            bgRect.anchoredPosition = new Vector2(10, -10);
            bgRect.sizeDelta = new Vector2(320, 300);

            backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.6f);

            // TMP Text
            GameObject textObj = new GameObject("DebugText");
            textObj.transform.SetParent(bgObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 4);
            textRect.offsetMax = new Vector2(-8, -4);

            displayText = textObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
                displayText.font = fontAsset;
            else
            {
                // TMP 기본 내장 폰트 명시적 로드 (프로젝트 기본이 한글 폰트일 수 있으므로)
                var defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (defaultFont != null)
                    displayText.font = defaultFont;
            }
            displayText.fontSize = fontSize;
            displayText.color = Color.white;
            displayText.alignment = TextAlignmentOptions.TopLeft;
            displayText.richText = false;
            displayText.textWrappingMode = TextWrappingModes.Normal;
            displayText.overflowMode = TextOverflowModes.Overflow;
            displayText.text = "초기화 중...";
        }

        /// <summary>
        /// 로컬 플레이어 참조 캐싱. 스폰 후 한 번만 찾으면 됨.
        /// </summary>
        private void CacheLocalPlayer()
        {
            if (localPlayer != null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    localPlayer = p.transform;
                    localSkillManager = p.GetComponentInChildren<SkillManager>();
                    localDamageable = p.GetComponent<IDamageable>();
                    return;
                }
            }
        }

        private void RefreshDisplay()
        {
            var sb = new System.Text.StringBuilder();

            // 게임 상태
            var gm = GameManager.Instance;
            if (gm != null)
            {
                string state = gm.CurrentState.ToString();
                int min = Mathf.FloorToInt(gm.GameTime / 60f);
                int sec = Mathf.FloorToInt(gm.GameTime % 60f);
                sb.AppendLine($"[{state}]  {min:00}:{sec:00}");
                sb.AppendLine($"Team Lv.{gm.TeamLevel}  EXP:{gm.TeamExp}");
            }
            else
            {
                sb.AppendLine("No GameManager");
            }

            sb.AppendLine();

            // HP
            if (localDamageable != null)
            {
                sb.AppendLine($"HP: {localDamageable.CurrentHP}/{localDamageable.MaxHP}");
            }
            else
            {
                sb.AppendLine("HP: --");
            }

            sb.AppendLine();

            // 스킬 목록
            if (localSkillManager != null)
            {
                sb.AppendLine($"Skills ({localSkillManager.SlotCount}/{localSkillManager.MaxSlots})");

                var skills = localSkillManager.EquippedSkills;
                for (int i = 0; i < skills.Count; i++)
                {
                    var s = skills[i];
                    string typeTag;
                    switch (s.Data.skillType)
                    {
                        case SwDreams.Features.Skill.Adapter.Data.SkillType.Active:  typeTag = "A"; break;
                        case SwDreams.Features.Skill.Adapter.Data.SkillType.Passive: typeTag = "P"; break;
                        case SwDreams.Features.Skill.Adapter.Data.SkillType.Chaos:   typeTag = "C"; break;
                        default:                     typeTag = "?"; break;
                    }

                    string maxTag = s.IsMaxLevel ? " MAX" : "";
                    sb.AppendLine($"  [{typeTag}] {s.Data.skillName} Lv.{s.Level}{maxTag}");
                }

                // 진화 대기
                var evos = localSkillManager.GetPendingEvolutions();
                if (evos.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"* Evolution Ready: {evos.Count}");
                }
            }
            else
            {
                sb.AppendLine("Skills: No Player");
            }

            sb.AppendLine();

            // 혼돈 스킬
            if (localPlayer != null)
            {
                var chaosManager = localPlayer.GetComponentInChildren<ChaosSkillManager>();
                if (chaosManager != null && chaosManager.ActiveEffects.Count > 0)
                {
                    sb.AppendLine($"Chaos ({chaosManager.ActiveEffects.Count})");
                    for (int i = 0; i < chaosManager.ActiveEffects.Count; i++)
                        sb.AppendLine($"  * {chaosManager.ActiveEffects[i]}");
                }
            }

            sb.AppendLine();

            // 네트워크
            string role = PhotonNetwork.IsMasterClient ? "Host" : "Client";
            int ping = PhotonNetwork.GetPing();
            sb.AppendLine($"{role}  Players:{PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}  Ping:{ping}ms");

            // 배경 크기 자동 조정
            displayText.text = sb.ToString();
            AdjustBackgroundSize();
        }

        /// <summary>
        /// 텍스트 길이에 맞게 배경 크기 자동 조정.
        /// </summary>
        private void AdjustBackgroundSize()
        {
            if (displayText == null || backgroundImage == null) return;

            displayText.ForceMeshUpdate();
            // GetPreferredValues: 잘림(Truncate) 관계없이 전체 콘텐츠 크기 반환
            var textSize = displayText.GetPreferredValues();
            var bgRect = backgroundImage.rectTransform;
            bgRect.sizeDelta = new Vector2(
                Mathf.Max(280, textSize.x + 20),
                Mathf.Max(100, textSize.y + 16)
            );
        }
    }
}