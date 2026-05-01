using UnityEngine;
using UnityEngine.UI;
using SwDreams.Features.Voice.Adapter;

namespace SwDreams.Features.Voice.Presentation
{
    /// <summary>
    /// InGameHUD 의 마이크 ON/OFF 버튼에 붙이는 브리지.
    /// VoiceController 가 PlayerStub 런타임 인스턴스에 붙어있어 인스펙터 드래그 불가하므로,
    /// 정적 LocalInstance 를 경유해 호출한다.
    ///
    /// 셋업:
    ///   1. InGameHUD 프리팹에 마이크 아이콘용 Button 추가
    ///   2. 본 컴포넌트 부착
    ///   3. Button.OnClick 인스펙터에 이 컴포넌트의 OnClick() 등록
    ///   4. (선택) iconImage / micOnSprite / micOffSprite 할당하면 음소거 상태에 따라 자동 토글
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MicToggleButton : MonoBehaviour
    {
        [Header("Visual (Optional)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Sprite micOnSprite;
        [SerializeField] private Sprite micOffSprite;

        [Tooltip("음소거 상태에서 아이콘 색을 살짝 어둡게.")]
        [SerializeField] private Color mutedTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color unmutedTint = Color.white;

        private void OnEnable()
        {
            VoiceController.OnLocalMuteChanged += RefreshVisual;
            // 시작 시 1회 동기화 (LocalInstance 가 아직 없을 수도 있음 — 그땐 기본 Unmuted).
            RefreshVisual(VoiceController.LocalInstance != null && VoiceController.LocalInstance.IsMuted);
        }

        private void OnDisable()
        {
            VoiceController.OnLocalMuteChanged -= RefreshVisual;
        }

        /// <summary>Button.OnClick 에서 호출.</summary>
        public void OnClick()
        {
            VoiceController.LocalInstance?.ToggleMute();
        }

        private void RefreshVisual(bool muted)
        {
            if (iconImage == null) return;

            if (muted && micOffSprite != null) iconImage.sprite = micOffSprite;
            else if (!muted && micOnSprite != null) iconImage.sprite = micOnSprite;

            iconImage.color = muted ? mutedTint : unmutedTint;
        }
    }
}
