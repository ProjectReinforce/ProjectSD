using UnityEngine;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Skill.Adapter
{
    /// <summary>
    /// 디버프 마커. 적에게 부착되는 시각적 표시 + 추가 피해 배율.
    ///
    /// 동작:
    /// 1. DebuffEffect가 적에게 AddComponent로 부착
    /// 2. duration 동안 활성 — 이 적은 damageAmplify만큼 추가 피해
    /// 3. duration 만료 시 자동 제거
    ///
    /// Enemy.TakeDamage() 호출 전에 DebuffMark 존재 여부를 확인하여
    /// 추가 피해를 적용하는 것은 호스트 판정 시점에서 처리.
    ///
    /// 설계 결정:
    /// - IPoolable이 아닌 AddComponent/Destroy 방식 사용
    ///   (적 개체에 동적 부착이므로 풀링보다 직접 관리가 적합)
    /// - 비주얼 마커는 자식 프리팹으로 생성 (effectPrefab)
    /// </summary>
    public class DebuffMark : MonoBehaviour
    {
        /// <summary>
        /// 이 적이 받는 추가 피해 배율. (1.3 = 130% 피해)
        /// Enemy 히트 판정 시 외부에서 참조.
        /// </summary>
        public float DamageAmplify { get; private set; } = 1f;

        private float duration;
        private float aliveTime;

        // 비주얼 마커 오브젝트
        private GameObject visualMarker;
        private GameObject cachedMarkerPrefab;

        // [Phase 5 진화: 역병 인형] 사망 시 전이
        private int spreadOnDeathCount;
        private bool subscribedToDeath;

        /// <summary>
        /// DebuffEffect에서 호출.
        /// </summary>
        public void Initialize(float damageAmplify, float duration,
            GameObject markerPrefab = null, int spreadOnDeathCount = 0)
        {
            DamageAmplify = damageAmplify;
            this.duration = duration;
            this.spreadOnDeathCount = spreadOnDeathCount;
            cachedMarkerPrefab = markerPrefab;
            aliveTime = 0f;

            // 비주얼 마커 생성
            if (markerPrefab != null)
            {
                visualMarker = PoolManager.Instance?.Get(markerPrefab);
                if (visualMarker != null)
                {
                    visualMarker.transform.SetParent(transform);
                    visualMarker.transform.localPosition = Vector3.up * 0.5f;
                    visualMarker.transform.localScale = Vector3.one;
                }
            }

            // 역병 전이: 사망 이벤트 구독
            if (spreadOnDeathCount > 0 && !subscribedToDeath)
            {
                var enemy = GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                if (enemy != null)
                {
                    enemy.OnDied += OnHostEnemyDied;
                    subscribedToDeath = true;
                }
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossFight)
                return;

            aliveTime += Time.deltaTime;
            if (aliveTime >= duration)
            {
                RemoveDebuff();
            }
        }

        /// <summary>
        /// 디버프 갱신 (중복 적용 시 타이머 리셋 + 배율 갱신).
        /// </summary>
        public void Refresh(float damageAmplify, float duration)
        {
            DamageAmplify = damageAmplify;
            this.duration = duration;
            aliveTime = 0f;
        }

        private void RemoveDebuff()
        {
            UnsubscribeDeath();

            if (visualMarker != null)
            {
                visualMarker.transform.SetParent(null);
                PoolManager.Instance?.Return(visualMarker);
                visualMarker = null;
            }

            Destroy(this);
        }

        /// <summary>
        /// [진화: 역병 인형] 저주 대상 사망 시 가까운 적에게 전이.
        /// </summary>
        private void OnHostEnemyDied()
        {
            if (spreadOnDeathCount <= 0) return;

            Vector2 deathPos = transform.position;
            int spread = 0;

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");

            // 거리순 정렬 (가장 가까운 적부터)
            System.Array.Sort(enemies, (a, b) =>
            {
                float dA = Vector2.Distance(deathPos, a.transform.position);
                float dB = Vector2.Distance(deathPos, b.transform.position);
                return dA.CompareTo(dB);
            });

            foreach (var e in enemies)
            {
                if (spread >= spreadOnDeathCount) break;
                if (!e.activeInHierarchy) continue;
                if (e == gameObject) continue; // 자기 자신 제외

                var existing = e.GetComponent<DebuffMark>();
                if (existing != null)
                {
                    existing.Refresh(DamageAmplify, duration);
                }
                else
                {
                    var mark = e.AddComponent<DebuffMark>();
                    mark.Initialize(DamageAmplify, duration, cachedMarkerPrefab, spreadOnDeathCount);
                }
                spread++;
            }
        }

        private void UnsubscribeDeath()
        {
            if (subscribedToDeath)
            {
                var enemy = GetComponent<SwDreams.Features.Enemy.Adapter.Enemy>();
                if (enemy != null)
                    enemy.OnDied -= OnHostEnemyDied;
                subscribedToDeath = false;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeDeath();

            if (visualMarker != null)
            {
                visualMarker.transform.SetParent(null);
                PoolManager.Instance?.Return(visualMarker);
                visualMarker = null;
            }
        }
    }
}
