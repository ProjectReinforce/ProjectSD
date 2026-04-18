using UnityEngine;
using SwDreams.Features.Character.Adapter;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 피격 파티클 이펙트 관리.
    /// 풀링 기반. 파티클 재생 완료 시 자동 반환.
    ///
    /// 프리팹 구성:
    /// - GameObject "HitEffect"
    ///   - ParticleSystem (Looping=false, StopAction=None)
    ///   - HitEffect 스크립트
    ///
    /// ParticleSystem 권장 설정 (플레이스홀더):
    ///   Duration: 0.3
    ///   Start Lifetime: 0.2~0.4
    ///   Start Speed: 2~4
    ///   Start Size: 0.1~0.2
    ///   Max Particles: 8
    ///   Emission: Burst 6~8개
    ///   Shape: Circle, Radius 0.1
    ///   Color over Lifetime: 흰색 → 투명
    ///   Renderer: Sorting Layer 맞춤
    ///
    /// 사용: HitEffect.Spawn(position)
    /// </summary>
    public class HitEffect : MonoBehaviour, IPoolable
    {
        private ParticleSystem ps;
        private float returnTimer;
        private bool isActive;

        private static GameObject effectPrefab;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
            if (ps == null)
                ps = GetComponentInChildren<ParticleSystem>();
        }

        /// <summary>
        /// 프리팹 등록. 최초 1회.
        /// </summary>
        public static void SetPrefab(GameObject prefab)
        {
            effectPrefab = prefab;
            PoolManager.Instance?.Prewarm(prefab, 15);
        }

        /// <summary>
        /// 피격 이펙트 스폰.
        /// </summary>
        public static void Spawn(Vector3 position)
        {
            if (PoolManager.Instance == null) return;

            // 프리팹 자동 등록
            if (effectPrefab == null)
            {
                var cfg = GameManager.Instance?.Config;
                if (cfg != null && cfg.hitEffectPrefab != null)
                    SetPrefab(cfg.hitEffectPrefab);
            }

            if (effectPrefab == null) return;

            var obj = PoolManager.Instance.Get(effectPrefab);
            var fx = obj.GetComponent<HitEffect>();
            if (fx != null)
                fx.Play(position);
        }

        private void Play(Vector3 position)
        {
            transform.position = position;
            isActive = true;

            if (ps != null)
            {
                ps.Clear();
                ps.Play();
                returnTimer = ps.main.duration + ps.main.startLifetime.constantMax;
            }
            else
            {
                // ParticleSystem 없으면 0.5초 후 반환
                returnTimer = 0.5f;
            }
        }

        private void Update()
        {
            if (!isActive) return;

            returnTimer -= Time.deltaTime;
            if (returnTimer <= 0f)
            {
                isActive = false;
                PoolManager.Instance?.Return(gameObject);
            }
        }

        // ===== IPoolable =====

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            isActive = true;
        }

        public void OnReturnToPool()
        {
            isActive = false;
            if (ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}