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

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            skillManager = GetComponentInChildren<SkillManager>();
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
                StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private System.Collections.IEnumerator HitFlashRoutine()
        {
            if (spriteRenderer == null) yield break;
            Color original = spriteRenderer.color;
            spriteRenderer.color = new Color(1f, 0.4f, 0.4f, spriteRenderer.color.a);
            yield return new WaitForSeconds(0.2f);
            spriteRenderer.color = original;
            hitFlashCoroutine = null;
        }

        // ===== 사망/부활 비주얼 =====

        private void OnDeadStateChanged(bool dead)
        {
            // 반투명 처리
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = dead ? 0.3f : 1f;
                spriteRenderer.color = c;
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