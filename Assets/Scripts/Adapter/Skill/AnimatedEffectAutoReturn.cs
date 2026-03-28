using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 스프라이트 애니메이션 이펙트의 자동 풀 반환.
    /// Animator의 클립이 1회 재생 완료되면 풀에 반환.
    ///
    /// 사용법:
    /// 1. 이펙트 프리팹에 이 스크립트 추가
    /// 2. Animator의 애니메이션 클립 Loop Time = false (체크 해제)
    /// 3. 풀링 사용 시 IPoolable 자동 처리
    ///
    /// Loop Time 해제가 불가능한 경우:
    /// useFixedDuration = true로 설정하고 duration 지정.
    /// </summary>
    public class AnimatedEffectAutoReturn : MonoBehaviour, IPoolable
    {
        [Header("설정")]
        [Tooltip("true면 애니메이션 길이 대신 고정 시간 사용")]
        [SerializeField] private bool useFixedDuration = false;
        [Tooltip("고정 시간 (useFixedDuration = true일 때만 사용)")]
        [SerializeField] private float duration = 0.5f;

        private Animator animator;
        private float timer;
        private bool isActive;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            isActive = true;
            timer = 0f;

            // 애니메이션 처음부터 재생
            if (animator != null)
                animator.Play(0, -1, 0f);
        }

        private void Update()
        {
            if (!isActive) return;

            if (useFixedDuration)
            {
                timer += Time.deltaTime;
                if (timer >= duration)
                    ReturnToPool();
            }
            else
            {
                // 현재 애니메이션 상태 확인
                if (animator == null)
                {
                    ReturnToPool();
                    return;
                }

                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                // normalizedTime >= 1.0 이면 1회 재생 완료
                if (stateInfo.normalizedTime >= 1f)
                    ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            isActive = false;
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
        }

        public void OnReturnToPool()
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }
}