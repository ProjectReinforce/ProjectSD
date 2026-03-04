using UnityEngine;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Skill
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

        // 비주얼 마커 오브젝트 (effectPrefab의 인스턴스)
        private GameObject visualMarker;

        /// <summary>
        /// DebuffEffect에서 호출.
        /// </summary>
        public void Initialize(float damageAmplify, float duration, GameObject markerPrefab = null)
        {
            DamageAmplify = damageAmplify;
            this.duration = duration;
            aliveTime = 0f;

            // 비주얼 마커 생성 (있으면)
            if (markerPrefab != null)
            {
                visualMarker = PoolManager.Instance?.Get(markerPrefab);
                if (visualMarker != null)
                {
                    visualMarker.transform.SetParent(transform);
                    visualMarker.transform.localPosition = Vector3.up * 0.5f; // 적 머리 위
                    visualMarker.transform.localScale = Vector3.one;
                }
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
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
            // 비주얼 마커 반환
            if (visualMarker != null)
            {
                visualMarker.transform.SetParent(null);
                PoolManager.Instance?.Return(visualMarker);
                visualMarker = null;
            }

            Destroy(this);
        }

        private void OnDestroy()
        {
            // 적이 먼저 죽어서 Destroy될 때 마커도 정리
            if (visualMarker != null)
            {
                visualMarker.transform.SetParent(null);
                PoolManager.Instance?.Return(visualMarker);
                visualMarker = null;
            }
        }
    }
}
