using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SwDreams.Features.UI.Adapter.Indicator;

namespace SwDreams.Features.UI.Presentation.Indicator
{
    public class WorldIndicatorView : MonoBehaviour
    {
        private enum Mode { InScreen, OffScreen }

        [Header("On-Screen (월드 스페이스)")]
        [SerializeField] private GameObject onScreenRoot;
        [SerializeField] private TMP_Text   onScreenName;

        [Header("Off-Screen (스크린 스페이스)")]
        [SerializeField] private GameObject offScreenRoot;
        [SerializeField] private TMP_Text   offScreenName;
        [SerializeField] private Image      arrowImage;
        [SerializeField] private Image      borderImage;
        [SerializeField] private float      edgePadding = 40f;

        private const float Margin = 0.05f;
        private const float NameOffsetY = 0.5f;

        private IWorldIndicatorTarget target;
        private Mode currentMode = Mode.OffScreen;
        private RectTransform onScreenRect;
        private RectTransform offScreenRect;

        public void Initialize(IWorldIndicatorTarget t, Canvas worldCanvas, Canvas screenCanvas)
        {
            target = t;

            if (onScreenRoot != null && worldCanvas != null)
                onScreenRoot.transform.SetParent(worldCanvas.transform, false);
            if (offScreenRoot != null && screenCanvas != null)
                offScreenRoot.transform.SetParent(screenCanvas.transform, false);

            onScreenRect  = onScreenRoot != null  ? onScreenRoot.GetComponent<RectTransform>()  : null;
            offScreenRect = offScreenRoot != null ? offScreenRoot.GetComponent<RectTransform>() : null;

            if (onScreenName != null)
            {
                onScreenName.text  = t.DisplayName;
                onScreenName.color = t.IndicatorColor;
            }
            if (offScreenName != null)
            {
                offScreenName.text  = t.DisplayName;
                offScreenName.color = Color.white;
            }
            if (borderImage != null) borderImage.color = t.IndicatorColor;
            if (arrowImage  != null) arrowImage.color  = t.IndicatorColor;
        }

        private void OnDestroy()
        {
            if (onScreenRoot != null)  Destroy(onScreenRoot);
            if (offScreenRoot != null) Destroy(offScreenRoot);
        }

        private void LateUpdate()
        {
            if (target == null || target.Transform == null || !target.IsActive)
            {
                if (onScreenRoot  != null) onScreenRoot.SetActive(false);
                if (offScreenRoot != null) offScreenRoot.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 vp = cam.WorldToViewportPoint(target.Transform.position);

            bool insideOuter  = vp.z > 0
                && vp.x > -Margin && vp.x < 1 + Margin
                && vp.y > -Margin && vp.y < 1 + Margin;
            bool insideScreen = vp.z > 0
                && vp.x >= 0 && vp.x <= 1
                && vp.y >= 0 && vp.y <= 1;

            if (currentMode == Mode.InScreen && !insideOuter) currentMode = Mode.OffScreen;
            else if (currentMode == Mode.OffScreen && insideScreen) currentMode = Mode.InScreen;

            bool showInScreen  = currentMode == Mode.InScreen
                              && target.Policy != IndicatorPolicy.OffScreenOnly;
            bool showOffScreen = currentMode == Mode.OffScreen;

            if (onScreenRoot  != null) onScreenRoot.SetActive(showInScreen);
            if (offScreenRoot != null) offScreenRoot.SetActive(showOffScreen);

            if (showInScreen)  UpdateOnScreen();
            if (showOffScreen) UpdateOffScreen(cam);
        }

        private void UpdateOnScreen()
        {
            if (onScreenRect == null) return;
            onScreenRect.position = target.Transform.position + Vector3.up * NameOffsetY;
        }

        private void UpdateOffScreen(Camera cam)
        {
            if (offScreenRect == null) return;

            Vector3 sp = cam.WorldToScreenPoint(target.Transform.position);
            if (sp.z < 0) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = ((Vector2)sp - center);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();

            float halfW = Screen.width  * 0.5f - edgePadding;
            float halfH = Screen.height * 0.5f - edgePadding;

            float dx = Mathf.Abs(dir.x) < 1e-4f ? 1e-4f : Mathf.Abs(dir.x);
            float dy = Mathf.Abs(dir.y) < 1e-4f ? 1e-4f : Mathf.Abs(dir.y);
            float t  = Mathf.Min(halfW / dx, halfH / dy);

            offScreenRect.position = center + dir * t;
            offScreenRect.localEulerAngles = new Vector3(0, 0,
                Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
