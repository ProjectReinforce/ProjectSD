using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 팝업 배경 블러 처리기.
    /// GameObject 활성화 시 프레임 끝까지 기다린 뒤 카메라를 RT로 렌더 → Kawase 블러 → RawImage로 표시.
    /// ScreenSpaceOverlay Canvas 의 UI 는 카메라가 렌더하지 않으므로 자동으로 제외됨.
    /// 팝업 오픈 시 GameState.Paused 로 게임 로직이 멈추므로 1회 캡처로 충분.
    ///
    /// 사용: 팝업 하이어라키 가장 뒤에 RawImage(Stretch)를 추가하고 이 컴포넌트만 붙이면 됨.
    /// 부모 팝업이 SetActive(true) 하면 OnEnable → 자동 블러, SetActive(false) 하면 RT 해제.
    ///
    /// 쉐이더: Assets/Resources/Shaders/UIBlur.shader (ProjectSD/UI/KawaseBlur)
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBackgroundBlur : MonoBehaviour
    {
        [Header("블러 파라미터")]
        [Tooltip("다운샘플 배율. 2 = 절반 해상도 (성능↑, 품질↓)")]
        [Range(1, 4)] [SerializeField] private int downsample = 2;

        [Tooltip("Kawase 블러 반복 횟수. 많을수록 부드러움")]
        [Range(1, 8)] [SerializeField] private int iterations = 4;

        [Tooltip("각 반복의 샘플 오프셋 시작값")]
        [SerializeField] private float startOffset = 1.0f;

        [Tooltip("반복마다 오프셋 증가량")]
        [SerializeField] private float offsetStep = 1.0f;

        [Header("연출")]
        [Tooltip("페이드 인 시간. 0 이면 즉시 표시")]
        [SerializeField] private float fadeInDuration = 0.25f;

        [Header("플랫폼 보정")]
        [Tooltip("Y 반전. cam.Render 방식은 보통 false, 렌더 결과가 뒤집히면 true 로")]
        [SerializeField] private bool flipY = false;

        [Tooltip("X 반전. 기본은 false")]
        [SerializeField] private bool flipX = false;

        [Header("렌더 소스")]
        [Tooltip("비워두면 Camera.main 사용")]
        [SerializeField] private Camera sourceCamera;

        private RawImage rawImage;
        private CanvasGroup canvasGroup;
        private Material blurMaterial;
        private RenderTexture finalRT;
        private Tween fadeTween;
        private Coroutine captureRoutine;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            canvasGroup = GetComponent<CanvasGroup>();

            var shader = Shader.Find("ProjectSD/UI/KawaseBlur");
            if (shader == null)
            {
                Debug.LogError("[UIBackgroundBlur] UIBlur 쉐이더를 찾을 수 없음. Assets/Resources/Shaders/UIBlur.shader 확인.");
                enabled = false;
                return;
            }
            blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnEnable()
        {
            if (blurMaterial == null) return;

            // 캡처 완료 전까지 투명 — 분신 방지
            canvasGroup.alpha = 0f;

            if (captureRoutine != null) StopCoroutine(captureRoutine);
            captureRoutine = StartCoroutine(CaptureAtEndOfFrame());
        }

        private void OnDisable()
        {
            if (captureRoutine != null)
            {
                StopCoroutine(captureRoutine);
                captureRoutine = null;
            }
            fadeTween?.Kill();
            ReleaseRT();
        }

        private IEnumerator CaptureAtEndOfFrame()
        {
            // 모든 LateUpdate + 카메라 렌더 완료 후 백버퍼에서 캡처 → 실제 화면과 픽셀 단위 일치
            yield return new WaitForEndOfFrame();

            CaptureAndBlur();

            fadeTween?.Kill();
            if (fadeInDuration > 0f)
            {
                canvasGroup.alpha = 0f;
                fadeTween = canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }

            captureRoutine = null;
        }

        private void OnDestroy()
        {
            fadeTween?.Kill();
            ReleaseRT();
            if (blurMaterial != null) Destroy(blurMaterial);
        }

        private void CaptureAndBlur()
        {
            Camera cam = sourceCamera != null ? sourceCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[UIBackgroundBlur] 캡처할 카메라가 없음. Camera.main 태그 확인.");
                return;
            }

            int sw = Screen.width;
            int sh = Screen.height;
            int w = Mathf.Max(1, sw / downsample);
            int h = Mathf.Max(1, sh / downsample);

            // 1) 카메라를 RT 로 렌더 — Overlay Canvas UI 는 카메라 렌더 대상이 아니므로 자동 제외
            var captureRT = RenderTexture.GetTemporary(sw, sh, 16, RenderTextureFormat.Default);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = captureRT;
            cam.Render();
            cam.targetTexture = prevTarget;

            // 2) 다운샘플 (+ 필요시 플립 보정)
            var srcRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            var dstRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            Vector2 scale = new Vector2(flipX ? -1f : 1f, flipY ? -1f : 1f);
            Vector2 offset = new Vector2(flipX ? 1f : 0f, flipY ? 1f : 0f);
            Graphics.Blit(captureRT, srcRT, scale, offset);
            RenderTexture.ReleaseTemporary(captureRT);

            // 3) Kawase 다중 패스 (ping-pong)
            RenderTexture a = srcRT;
            RenderTexture b = dstRT;
            for (int i = 0; i < iterations; i++)
            {
                blurMaterial.SetFloat("_Offset", startOffset + offsetStep * i);
                Graphics.Blit(a, b, blurMaterial);
                (a, b) = (b, a);
            }

            // 4) 결과 RT 보존 (RawImage 표시용)
            ReleaseRT();
            finalRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            Graphics.Blit(a, finalRT);
            rawImage.texture = finalRT;

            RenderTexture.ReleaseTemporary(srcRT);
            RenderTexture.ReleaseTemporary(dstRT);
        }

        private void ReleaseRT()
        {
            if (finalRT != null)
            {
                if (rawImage != null) rawImage.texture = null;
                RenderTexture.ReleaseTemporary(finalRT);
                finalRT = null;
            }
        }
    }
}
