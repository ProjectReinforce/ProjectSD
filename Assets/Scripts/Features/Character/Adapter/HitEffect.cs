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
        private ParticleSystem[] allParticleSystems;
        private AudioSource audioSource;
        private float returnTimer;
        private bool isActive;

        private static GameObject effectPrefab;

        private void Awake()
        {
            // 부모-자식 모든 ParticleSystem 캐싱 — Play(true) 가 자식까지 발동시키지 않는
            // edge case (자식 ps 가 별도 GameObject 인 경우) 방어 차원에서 명시적 순회.
            allParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            ps = allParticleSystems != null && allParticleSystems.Length > 0
                ? allParticleSystems[0] : null;
            audioSource = GetComponentInChildren<AudioSource>(true);
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
            // 위치 먼저 set 후 활성화 — AudioSource PlayOnAwake=true 라도 정확한 위치에서 재생.
            transform.position = position;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            isActive = true;

            // 모든 ParticleSystem 명시 Clear+Play — ps.Play(true) 가 자식 hierarchy 를 잡지 못하는
            // edge case (자식이 별도 GameObject + 별도 ps) 까지 확실히 처리.
            if (allParticleSystems != null)
            {
                for (int i = 0; i < allParticleSystems.Length; i++)
                {
                    var p = allParticleSystems[i];
                    if (p == null) continue;
                    p.Clear(true);
                    p.Play(true);
                }
            }

            // AudioSource PlayOnAwake=false 인 경우 명시 재생 보장.
            if (audioSource != null && !audioSource.isPlaying && audioSource.playOnAwake == false)
                audioSource.Play();

            if (ps != null)
                returnTimer = ps.main.duration + ps.main.startLifetime.constantMax;
            else
                returnTimer = 0.5f;
        }

        private void Update()
        {
            if (!isActive) return;

            // B7: 레벨업/메뉴 일시정지 중엔 ParticleSystem 도 정지 + returnTimer 도 정지.
            //     복귀 시 자동 재개. (다른 비주얼 컴포넌트와 동일 패턴)
            var gm = GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.BossFight)
            {
                if (allParticleSystems != null)
                {
                    for (int i = 0; i < allParticleSystems.Length; i++)
                    {
                        var p = allParticleSystems[i];
                        if (p != null && p.isPlaying) p.Pause(true);
                    }
                }
                return;
            }
            else
            {
                // 복귀 시 일시정지된 ps 재생.
                if (allParticleSystems != null)
                {
                    for (int i = 0; i < allParticleSystems.Length; i++)
                    {
                        var p = allParticleSystems[i];
                        if (p != null && p.isPaused) p.Play(true);
                    }
                }
            }

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
            // SetActive 는 Play(position) 에서 정확한 위치로 옮긴 뒤 호출.
            // N2 해결: prefab 기본 위치(0,0) 에서의 자동 emit + AudioSource 자동 재생을 원천 차단.
            // 호출 흐름이 Spawn → PoolManager.Get → OnSpawnFromPool → fx.Play(position) 로 동기적이라
            // GameObject 가 "비활성 상태로 잠시 반환되는" 윈도우는 한 호출 스택 내에 닫힌다.
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