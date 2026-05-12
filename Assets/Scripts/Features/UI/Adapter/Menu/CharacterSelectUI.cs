using UnityEngine;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Unlock.Adapter;
using UnityEngine.UI;
using TMPro;
using SwDreams.Shared.Data;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실 캐릭터 선택 팝업 UI.
    ///
    /// 변경 전: 항상 노출, 선택 즉시 WaitingRoomPanelController에 직접 전달.
    /// 변경 후: Open()/Close() 패턴으로 팝업화, 확인 버튼을 눌러야 최종 적용.
    ///
    /// 설계 원칙:
    ///   SRP — 데이터 바인딩 + 선택 상태 관리 + 확인/취소만 담당.
    ///          팝업의 열기/닫기 시점 결정은 WaitingRoomPanelController가 한다.
    ///   DIP — WaitingRoomPanelController를 직접 참조하지 않고
    ///          ICharacterSelectCallback 인터페이스를 통해 결과를 전달한다.
    ///   OCP — CharacterDatabase SO만 교체하면 캐릭터 목록이 바뀐다.
    ///
    /// Inspector 셋업:
    ///   1. WaitingRoomPanel 하위에 CharacterSelectPanel (비활성 상태) 배치.
    ///   2. 이 스크립트를 CharacterSelectPanel에 부착.
    ///   3. characterDB 연결.
    ///   4. characterButtons 배열에 미리 만든 버튼 3개를 순서대로 연결.
    ///   5. confirmButton에 "확인" 버튼 연결.
    ///   6. callback은 WaitingRoomPanelController가 Open() 호출 시 주입.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private CharacterDatabase characterDB;

        [Header("버튼 (씬에 미리 배치된 3개)")]
        [Tooltip("순서대로 characterDB.characters[0], [1], [2]에 대응")]
        [SerializeField] private Button[] characterButtons;

        [Header("확인/취소")]
        [SerializeField] private Button confirmButton;

        [Header("선택 표시")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);

        private ICharacterSelectCallback callback;
        private int pendingIndex = -1;
        private int pendingCharacterId = -1;

        /// <summary>
        /// 현재 확정(Confirm)된 캐릭터 ID. 아직 확정 전이면 -1.
        /// WaitingRoomPanelController가 현재 적용된 캐릭터를 조회할 때 사용.
        /// </summary>
        public int ConfirmedCharacterId { get; private set; } = -1;

        /// <summary>
        /// 팝업을 열고 콜백을 주입한다.
        /// 왜 Open 메서드에서 콜백을 받는가:
        ///   - Inspector 직렬화로 인터페이스를 받을 수 없으므로 코드 주입이 필요하다.
        ///   - 열 때마다 콜백을 갱신할 수 있어 유연하다.
        /// </summary>
        public void Open(ICharacterSelectCallback selectCallback)
        {
            callback = selectCallback;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 팝업을 닫는다. 준비 상태 진입 시 강제 닫기에도 사용.
        /// pending 선택을 버리지 않고 유지한다 (다시 열면 이전 선택이 보임).
        /// </summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 팝업이 현재 열려 있는지 여부.
        /// WaitingRoomPanelController가 준비 상태 진입 시 확인용.
        /// </summary>
        public bool IsOpen => gameObject.activeSelf;

        private void OnEnable()
        {
            if (characterDB == null || characterDB.characters == null)
            {
                Debug.LogWarning("[CharacterSelectUI] CharacterDatabase가 연결되지 않았습니다.");
                return;
            }

            BindButtons();

            // 이전에 선택한 것이 없으면 첫 번째 캐릭터를 기본 선택
            if (pendingIndex < 0 && characterButtons != null && characterButtons.Length > 0
                && characterDB.characters.Length > 0)
            {
                SelectCharacter(characterDB.characters[0].id, 0);
            }
            else
            {
                // 이전 선택 상태 복원
                UpdateButtonVisuals();
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnClickConfirm);
            }
        }

        private void OnDisable()
        {
            UnbindButtons();

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnClickConfirm);
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 시 호출.
        /// pending 선택을 confirmed로 확정하고, 콜백을 통해 외부에 통보한 뒤 팝업을 닫는다.
        ///
        /// 왜 "선택"과 "확정"을 분리하는가:
        ///   - 사용자가 여러 캐릭터를 눌러보며 비교할 수 있다 (미리보기).
        ///   - 확인을 눌러야 네트워크에 반영되므로, 불필요한 네트워크 트래픽을 줄인다.
        ///   - 준비 상태에서 팝업이 강제 닫히면 pending은 버려지고 이전 confirmed가 유지된다.
        /// </summary>
        private void OnClickConfirm()
        {
            if (pendingCharacterId < 0)
            {
                Debug.LogWarning("[CharacterSelectUI] 선택된 캐릭터가 없습니다.");
                return;
            }

            ConfirmedCharacterId = pendingCharacterId;
            callback?.OnCharacterConfirmed(ConfirmedCharacterId);
            Close();
        }

        private void BindButtons()
        {
            if (characterButtons == null) return;

            var characters = characterDB.characters;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] == null) continue;
                if (i >= characters.Length || characters[i] == null) continue;

                var data = characters[i];

                // 메타 언락: 자기 PC 기준 자기 진행도로 직접 결정 (D5).
                // unlockConditions 비어있으면 처음부터 해금. 조건 있고 미충족이면 잠금 처리.
                bool unlocked = true;
                if (data.unlockConditions != null && data.unlockConditions.Count > 0)
                {
                    var tracker = UnlockTracker.Instance;
                    unlocked = tracker != null && tracker.IsCharacterUnlocked(data.id);
                }

                // portrait 세팅
                var portraitTransform = characterButtons[i].transform.Find("Portrait");
                if (portraitTransform != null)
                {
                    var img = portraitTransform.GetComponent<Image>();
                    if (img != null)
                    {
                        if (unlocked && data.portrait != null)
                        {
                            img.sprite = data.portrait;
                            img.color = Color.white;
                        }
                        else if (!unlocked)
                        {
                            // 잠금 상태 — 어둡게 처리 (별도 lock 아이콘 추가는 후속).
                            if (data.portrait != null) img.sprite = data.portrait;
                            img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                        }
                    }
                }

                // displayName 세팅
                var nameTransform = characterButtons[i].transform.Find("Name");
                if (nameTransform != null)
                {
                    var text = nameTransform.GetComponent<TMP_Text>();
                    if (text != null)
                        text.text = unlocked ? data.displayName : "???";
                }

                // 클릭 리스너 — 잠금 상태면 클릭 비활성화.
                characterButtons[i].interactable = unlocked;
                int capturedId = data.id;
                int capturedIndex = i;
                if (unlocked)
                {
                    characterButtons[i].onClick.AddListener(() =>
                    {
                        SelectCharacter(capturedId, capturedIndex);
                    });
                }
            }
        }

        private void UnbindButtons()
        {
            if (characterButtons == null) return;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] != null)
                    characterButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void SelectCharacter(int characterId, int buttonIndex)
        {
            pendingIndex = buttonIndex;
            pendingCharacterId = characterId;
            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (characterButtons == null) return;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] == null) continue;

                var colors = characterButtons[i].colors;
                bool isSelected = (i == pendingIndex);
                colors.normalColor = isSelected ? selectedColor : normalColor;
                colors.highlightedColor = isSelected
                    ? selectedColor
                    : new Color(normalColor.r + 0.1f, normalColor.g + 0.1f, normalColor.b + 0.1f);
                characterButtons[i].colors = colors;
            }
        }
    }
}
