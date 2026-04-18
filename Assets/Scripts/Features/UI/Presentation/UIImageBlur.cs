using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SwDreams.Features.UI.Presentation
{
    /// <summary>
    /// 이 스크립트가 붙은 RawImage의 현재 texture 를 Kawase 블러로 사전 처리 → 블러된 결과로 교체.
    /// UIBackgroundBlur 가 "게임 화면"을 블러하는 반면, 이 컴포넌트는 "UI 상 특정 이미지"를 블러한다.
    ///
    /// 사용:
    /// 1) RawImage 에 원본 텍스처(Sprite 가 아닌 Texture) 할당
    /// 2) 같은 오브젝트에 이 컴포넌트 추가
    /// 3) 활성화되면 자동으로 블러 처리
    ///
    /// Image(Sprite 기반) 에는 적용 불가 — RawImage 필요.
    /// 쉐이더: Assets/Resources/Shaders/UIBlur.shader (ProjectSD/UI/KawaseBlur)
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class UIImageBlur : MonoBehaviour
    {
        [Header("블러 파라미터")]
        [Tooltip("다운샘플 배율. 2 = 절반 해상도")]
        [Range(1, 4)] [SerializeField] private int downsample = 2;

        [Tooltip("Kawase 블러 반복 횟수")]
        [Range(1, 8)] [SerializeField] private int iterations = 4;

        [Tooltip("각 반복의 샘플 오프셋 시작값")]
        [SerializeField] private float startOffset = 1.0f;

        [Tooltip("반복마다 오프셋 증가량")]
        [SerializeField] private float offsetStep = 1.0f;

        [Header("소스 텍스처")]
        [Tooltip("비워두면 RawImage 에 설정된 texture 를 소스로 사용")]
        [SerializeField] private Texture overrideSource;

        [Header("연출")]
        [Tooltip("페이드 인 시간. 0 이면 즉시 표시")]
        [SerializeField] private float fadeInDuration = 0f;

        private RawImage rawImage;
        private CanvasGroup canvasGroup;
        private Material blurMaterial;
        private RenderTexture finalRT;
        private Texture originalTexture;
        private Tween fadeTween;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            canvasGroup = GetComponent<CanvasGroup>(); // optional

            originalTexture = rawImage.texture;

            var shader = Shader.Find("ProjectSD/UI/KawaseBlur");
            if (shader == null)
            {
                Debug.LogError("[UIImageBlur] UIBlur 쉐이더를 찾을 수 없음. Assets/Resources/Shaders/UIBlur.shader 확인.");
                enabled = false;
                return;
            }
            blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnEnable()
        {
            if (blurMaterial == null) return;

            Texture src = overrideSource != null ? overrideSource : originalTexture;
            if (src == null)
            {
                Debug.LogWarning("[UIImageBlur] 블러할 소스 텍스처가 없음. RawImage.texture 또는 overrideSource 설정 필요.");
                return;
            }

            ApplyBlur(src);

            if (canvasGroup != null && fadeInDuration > 0f)
            {
                fadeTween?.Kill();
                canvasGroup.alpha = 0f;
                fadeTween = canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }
        }

        private void OnDisable()
        {
            fadeTween?.Kill();
            // 원본 복구 + 블러 RT 해제
            if (rawImage != null) rawImage.texture = originalTexture;
            ReleaseRT();
        }

        private void OnDestroy()
        {
            fadeTween?.Kill();
            ReleaseRT();
            if (blurMaterial != null) Destroy(blurMaterial);
        }

        private void ApplyBlur(Texture src)
        {
            int w = Mathf.Max(1, src.width / downsample);
            int h = Mathf.Max(1, src.height / downsample);

            var srcRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            var dstRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);

            // 1) 소스를 다운샘플
            Graphics.Blit(src, srcRT);

            // 2) Kawase 다중 패스 (ping-pong)
            RenderTexture a = srcRT;
            RenderTexture b = dstRT;
            for (int i = 0; i < iterations; i++)
            {
                blurMaterial.SetFloat("_Offset", startOffset + offsetStep * i);
                Graphics.Blit(a, b, blurMaterial);
                (a, b) = (b, a);
            }

            // 3) 결과 보존 + RawImage 교체
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
                RenderTexture.ReleaseTemporary(finalRT);
                finalRT = null;
            }
        }

        /// <summary>
        /// 런타임에 소스 텍스처를 교체하고 재블러.
        /// </summary>
        public void SetSource(Texture newSource)
        {
            overrideSource = newSource;
            if (isActiveAndEnabled && blurMaterial != null)
                ApplyBlur(newSource);
        }
    }
}
