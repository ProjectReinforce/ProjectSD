using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using SwDreams.Domain;
using SwDreams.Adapter.Entity;
using SwDreams.Adapter.Manager;

namespace SwDreams.Presentation
{
    /// <summary>
    /// 보스 체력 바. 화면 상단 고정.
    /// 페이즈 구분선(60%, 30%) 표시.
    ///
    /// BossSpawner.CurrentBoss를 폴링하여 보스 등장 감지.
    /// Boss.OnHealthChanged, OnPhaseChanged 이벤트 구독.
    ///
    /// 셋업:
    /// Canvas 하위에 "BossHealthBar" 오브젝트.
    /// - Slider (체력 바)
    /// - TMP_Text (보스 이름)
    /// - RectTransform (phase2Marker, phase3Marker — 구분선 위치)
    /// 비활성 상태로 시작 → 보스 등장 시 활성화.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text healthPercentText;
        [SerializeField] private Image fillImage;

        [Header("페이즈 구분선")]
        [SerializeField] private RectTransform phase2Marker; // 60% 위치
        [SerializeField] private RectTransform phase3Marker; // 30% 위치

        [Header("색상")]
        [SerializeField] private Color phase1Color = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color phase2Color = new Color(0.9f, 0.7f, 0.1f);
        [SerializeField] private Color phase3Color = new Color(0.9f, 0.2f, 0.2f);

        private Boss currentBoss;
        private bool isTracking = false;

        private void Start()
        {
            // 활성 상태 유지 (Update 폴링 필요)
            // 비주얼만 숨김
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
        }

        private void Update()
        {
            // 보스 등장 감지
            if (!isTracking)
            {
                if (BossSpawner.Instance != null && BossSpawner.Instance.CurrentBoss != null)
                {
                    AttachToBoss(BossSpawner.Instance.CurrentBoss);
                }
            }
        }

        private void OnDestroy()
        {
            DetachFromBoss();
        }

        // ===== 보스 연결 =====

        private void AttachToBoss(Boss boss)
        {
            currentBoss = boss;
            isTracking = true;
            SetVisible(true);

            // 이름
            if (bossNameText != null && boss.Data != null)
                bossNameText.text = boss.Data.bossName;

            // 초기 HP
            UpdateHealth(boss.CurrentHP, boss.MaxHP);

            // 페이즈 구분선 — 레이아웃 계산 후 배치
            if (boss.Data != null)
                StartCoroutine(SetMarkersDelayed(boss.Data.phase2Threshold, boss.Data.phase3Threshold));

            // 이벤트 구독
            boss.OnHealthChanged += UpdateHealth;
            boss.OnPhaseChanged += OnPhaseChanged;
            boss.OnDied += OnBossDied;

            // 등장 연출
            transform.localScale = new Vector3(1f, 0f, 1f);
            transform.DOScaleY(1f, 0.5f).SetEase(Ease.OutBack);
        }

        private void DetachFromBoss()
        {
            if (currentBoss != null)
            {
                currentBoss.OnHealthChanged -= UpdateHealth;
                currentBoss.OnPhaseChanged -= OnPhaseChanged;
                currentBoss.OnDied -= OnBossDied;
                currentBoss = null;
            }
            isTracking = false;
        }

        // ===== 갱신 =====

        private void UpdateHealth(int current, int max)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }

            if (healthPercentText != null)
            {
                float percent = max > 0 ? (float)current / max * 100f : 0f;
                healthPercentText.text = $"{percent:F0}%";
            }
        }

        private void OnPhaseChanged(BossPhase newPhase)
        {
            // 색상 변경
            Color targetColor = phase1Color;
            switch (newPhase)
            {
                case BossPhase.Phase2: targetColor = phase2Color; break;
                case BossPhase.Phase3: targetColor = phase3Color; break;
            }

            if (fillImage != null)
                fillImage.DOColor(targetColor, 0.3f);

            // 화면 흔들림 연출
            transform.DOShakePosition(0.3f, 5f, 20).SetUpdate(true);
        }

        private void OnBossDied()
        {
            // 퇴장 연출
            transform.DOScaleY(0f, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    DetachFromBoss();
                    SetVisible(false);
                });
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 체력 바 위에 페이즈 구분선 위치 설정.
        /// threshold = 0.6이면 체력 바의 60% 위치에 마커.
        /// </summary>
        private void SetMarkerPosition(RectTransform marker, float threshold)
        {
            if (marker == null || healthSlider == null) return;

            var sliderRect = healthSlider.GetComponent<RectTransform>();
            float width = sliderRect.rect.width;
            float xPos = width * threshold;

            marker.anchoredPosition = new Vector2(xPos, marker.anchoredPosition.y);
        }

        private System.Collections.IEnumerator SetMarkersDelayed(float p2, float p3)
        {
            // stretch 레이아웃에서 RectTransform.rect.width가 0일 수 있음
            // LayoutRebuilder로 강제 계산 후 배치
            yield return null; // 1프레임 대기

            var sliderRect = healthSlider != null ? healthSlider.GetComponent<RectTransform>() : null;
            if (sliderRect != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(sliderRect);

            yield return null; // 재계산 후 1프레임 더 대기

            SetMarkerPosition(phase2Marker, p2);
            SetMarkerPosition(phase3Marker, p3);
        }
    }
}