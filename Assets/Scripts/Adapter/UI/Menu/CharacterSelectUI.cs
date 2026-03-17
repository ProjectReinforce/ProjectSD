using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SwDreams.Data;

namespace Adapter.UI.Menu
{
    /// <summary>
    /// 대기실 캐릭터 선택 UI.
    /// 미리 배치된 3개 버튼에 CharacterDatabase의 데이터를 바인딩.
    /// 선택 시 WaitingRoomPanelController.OnSelectCharacter(id) 호출.
    ///
    /// 설계 의도:
    ///   - 버튼 레이아웃은 디자이너가 씬에서 직접 배치 (위치/크기 자유).
    ///   - 이 스크립트는 데이터 바인딩 + 선택 전달만 담당 (SRP).
    ///   - CharacterDatabase SO만 교체하면 표시 내용이 바뀜 (OCP).
    ///
    /// 셋업:
    ///   1. WaitingRoomPanel 하위에 빈 GameObject "CharacterSelect" 생성.
    ///   2. 이 스크립트 부착.
    ///   3. Inspector에서 characterDB, waitingRoom 연결.
    ///   4. characterButtons 배열에 미리 만든 버튼 3개를 순서대로 연결.
    ///      → buttons[0] = 캐릭터 A (id:0), buttons[1] = B (id:1), buttons[2] = C (id:2)
    ///   5. 각 버튼 하위에 "Portrait"(Image), "Name"(TMP_Text)이 있으면 자동 세팅.
    ///      없어도 선택 기능은 정상 동작.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private CharacterDatabase characterDB;

        [Header("참조")]
        [SerializeField] private WaitingRoomPanelController waitingRoom;

        [Header("버튼 (씬에 미리 배치된 3개)")]
        [Tooltip("순서대로 characterDB.characters[0], [1], [2]에 대응")]
        [SerializeField] private Button[] characterButtons;

        [Header("선택 표시")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f, 1f);

        private int selectedIndex = -1;

        private void OnEnable()
        {
            if (characterDB == null || characterDB.characters == null)
            {
                Debug.LogWarning("[CharacterSelectUI] CharacterDatabase가 연결되지 않았습니다.");
                return;
            }

            BindButtons();

            // 첫 번째 캐릭터 자동 선택
            if (characterButtons != null && characterButtons.Length > 0)
            {
                SelectCharacter(characterDB.characters[0].id, 0);
            }
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        /// <summary>
        /// 미리 배치된 버튼에 데이터 바인딩 + 클릭 리스너 등록.
        /// </summary>
        private void BindButtons()
        {
            if (characterButtons == null) return;

            var characters = characterDB.characters;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] == null) continue;
                if (i >= characters.Length || characters[i] == null) continue;

                var data = characters[i];

                // portrait 세팅
                var portraitTransform = characterButtons[i].transform.Find("Portrait");
                if (portraitTransform != null && data.portrait != null)
                {
                    var img = portraitTransform.GetComponent<Image>();
                    if (img != null) img.sprite = data.portrait;
                }

                // displayName 세팅
                var nameTransform = characterButtons[i].transform.Find("Name");
                if (nameTransform != null)
                {
                    var text = nameTransform.GetComponent<TMP_Text>();
                    if (text != null) text.text = data.displayName;
                }

                // 클릭 리스너
                int capturedId = data.id;
                int capturedIndex = i;
                characterButtons[i].onClick.AddListener(() =>
                {
                    SelectCharacter(capturedId, capturedIndex);
                });
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

            selectedIndex = -1;
        }

        private void SelectCharacter(int characterId, int buttonIndex)
        {
            selectedIndex = buttonIndex;

            if (waitingRoom != null)
                waitingRoom.OnSelectCharacter(characterId);

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (characterButtons == null) return;

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] == null) continue;

                var colors = characterButtons[i].colors;
                bool isSelected = (i == selectedIndex);
                colors.normalColor = isSelected ? selectedColor : normalColor;
                colors.highlightedColor = isSelected
                    ? selectedColor
                    : new Color(normalColor.r + 0.1f, normalColor.g + 0.1f, normalColor.b + 0.1f);
                characterButtons[i].colors = colors;
            }
        }
    }
}
