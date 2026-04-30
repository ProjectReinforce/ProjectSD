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
                // 긴 i-frame: 첫 빨간 플래시 (피격 인지) + 그 후 alpha 깜빡임 (무적 표현).
                // i-frame 패시브로 길어진 경우에도 피격 자체가 명확히 인지되도록 첫 한 번은 빨간색.
                Color hitColor = new Color(1f, 0.4f, 0.4f, originColor.a);
                spriteRenderer.color = hitColor;
                yield return new WaitForSeconds(singleFlash);

                float elapsed = singleFlash;
                bool dim = false;
                Color dimColor = new Color(originColor.r, originColor.g, originColor.b, 0.4f);

                while (elapsed < iFrameDuration)
                {
                    spriteRenderer.color = dim ? dimColor : originColor;
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

            // 반투명 처리 — RGB 도 originColor 로 복원해 빨간색 잔여 방지.
            if (spriteRenderer != null)
            {
                if (dead)
                {
                    spriteRenderer.color = new Color(originColor.r, originColor.g, originColor.b, 0.3f);
                }
                else
                {
                    spriteRenderer.color = originColor;
                }
            }

            // 스킬 일시정지/재개
            if (skillManager != null)
            {
                if (dead) skillManager.PauseAllSkills();
                else skillManager.ResumeAllSkills();
            }
        }
    }
}