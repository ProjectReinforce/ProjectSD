using UnityEngine;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 비주얼 피드백. 피격 플래시, 사망 반투명 등.
    ///
    /// [Phase 7 리팩토링] Step 2-3: PlayerStub에서 분리.
    /// PlayerHealth의 이벤트를 구독하여 비주얼 반영.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Coroutine hitFlashCoroutine;
        private SkillManager skillManager;

        // N3: 피격 후 빨간색 고착 방지.
        // 이전엔 OnHit 시점에 spriteRenderer.color 를 캡처했는데, 0.2s 안에 재피격하면
        // 캡처값이 빨간색이 돼 영구 고착됐다. 정적 originColor 를 Awake 에서 한 번 캡처해 사용.
        private Color originColor = Color.white;

        // R7: PlayerHealth.IFrameDuration 길이만큼 깜빡임. 0 이면 기존 0.2s flash 유지.
        private PlayerHealth playerHealth;
        private PlayerStats playerStats;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            skillManager = GetComponentInChildren<SkillManager>();
            playerHealth = GetComponent<PlayerHealth>();
            playerStats = GetComponent<PlayerStats>();

            if (spriteRenderer != null)
                originColor = spriteRenderer.color;
        }

        /// <summary>
        /// PlayerHealth 이벤트에 바인딩. PlayerStub.Start()에서 호출.
        /// </summary>
        public void Bind(PlayerHealth health)
        {
            if (health == null) return;
            health.OnHit += OnHit;
            health.OnDeadStateChanged += OnDeadStateChanged;
        }

        private void OnDestroy()
        {
            // 이벤트 해제는 PlayerHealth.OnDestroy보다 먼저 될 수 있으므로 방어적 처리
            var health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.OnHit -= OnHit;
                health.OnDeadStateChanged -= OnDeadStateChanged;
            }
        }

        // ===== 피격 플래시 =====

        private void OnHit(int damage)
        {
            if (damage <= 0) return;
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                // N3: 이전 routine 중단 시 색을 즉시 원본으로 복원해야
                //     다음 routine 의 캡처값이 빨간색이 되는 고착을 방지.
                if (spriteRenderer != null)
                    spriteRenderer.color = originColor;
            }

            // R7: i-frame 길이가 있으면 그 길이만큼 깜빡임. 없으면 기본 0.2s flash.
            float iFrame = playerStats != null ? playerStats.IFrameDuration : 0f;
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine(iFrame));
        }

        private System.Collections.IEnumerator HitFlashRoutine(float iFrameDuration)
        {
            if (spriteRenderer == null) yield break;

            const float singleFlash = 0.1f;

            if (iFrameDuration <= singleFlash)
            {
                // 짧은 i-frame: 단일 빨간색 플래시 (기존 동작 유지)
                spriteRenderer.color = new Color(1f, 0.4f, 0.4f, originColor.a);
                yield return new WaitForSeconds(Mathf.Max(0.2f, iFrameDuration));
                spriteRenderer.color = originColor;
            }
            else
            {
                // 긴 i-frame: 전체 시간 동안 빨간색을 유지한 채 alpha 만 깜빡임.
                // 빨간 깜빡임 시간 == i-frame 시간 (피격 인지 + 무적 표현 통합).
                Color hitColor = new Color(1f, 0.4f, 0.4f, originColor.a);
                Color hitColorDim = new Color(1f, 0.4f, 0.4f, 0.4f);

                float elapsed = 0f;
                bool dim = false;

                while (elapsed < iFrameDuration)
                {
                    spriteRenderer.color = dim ? hitColorDim : hitColor;
                    dim = !dim;
                    yield return new WaitForSeconds(singleFlash);
                    elapsed += singleFlash;
                }
                spriteRenderer.color = originColor;
            }

            hitFlashCoroutine = null;
        }

        // ===== 사망/부활 비주얼 =====

        private void OnDeadStateChanged(bool dead)
        {
            // 진행 중인 hit flash 가 있으면 중단 (사망/부활은 더 우선).
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            // 사망/부활 시 색상은 originColor 로 복원 (빨간색 hit flash 잔여 방지).
            // 이전엔 dead 시 alpha=0.3 반투명 처리 — 사망 애니 도입 후 Die 클립이 흐릿하게 재생되는
            // 충돌 + 부활 대기 UI(DeathOverlayUI) 가 별도 시각 표시 담당이라 redundant. 제거.
            if (spriteRenderer != null)
                spriteRenderer.color = originColor;

            // 스킬 일시정지/재개
            if (skillManager != null)
            {
                if (dead) skillManager.PauseAllSkills();
                else skillManager.ResumeAllSkills();
            }
        }
    }
}