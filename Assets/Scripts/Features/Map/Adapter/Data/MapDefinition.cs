using UnityEngine;

namespace SwDreams.Features.Map.Adapter.Data
{
    /// <summary>
    /// 한 개의 맵 정의. 방 생성 시 호스트가 선택하여 Room.CustomProperties[mapId] 에 id 저장.
    /// 게임 시작 시 SceneTransitionManager 가 id → gameSceneName 매핑 후 해당 씬 로드.
    ///
    /// 현재 맵은 1개 (default → GameScene). 미래 추가 시 SO 생성 + MapDatabase 에 등록만 하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "MapDefinition", menuName = "SwDreams/Map/MapDefinition")]
    public class MapDefinition : ScriptableObject
    {
        [Tooltip("네트워크 식별자. 영문 소문자/숫자/언더스코어 권장. 예: default, forest_01. 절대 변경 금지(저장된 방 props 와 매칭).")]
        [SerializeField] private string id = "default";

        [Tooltip("UI 표시용 이름 (방 리스트, 방 생성 패널). 한글 가능.")]
        [SerializeField] private string displayName = "기본 맵";

        [Tooltip("이 맵 선택 시 로드할 게임씬 이름. 빌드 설정의 Scene 이름과 일치해야 함.")]
        [SerializeField] private string gameSceneName = "GameScene";

        [Tooltip("방 리스트/생성 패널에 표시할 미리보기 이미지 (선택).")]
        [SerializeField] private Sprite previewSprite;

        public string Id => id;
        public string DisplayName => displayName;
        public string GameSceneName => gameSceneName;
        public Sprite PreviewSprite => previewSprite;
    }
}
