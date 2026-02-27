using UnityEngine;
using Photon.Pun;
using SwDreams.Domain.Interfaces;
using SwDreams.Adapter.Manager;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 경험치 오브. 자석 흡수 + 획득 처리.
    /// 모든 클라이언트에서 로컬 생성 (PhotonView 없음).
    /// 획득 판정은 호스트만 처리.
    /// 
    /// 프리팹 구성:
    /// - ExperienceOrb (이 스크립트)
    /// - CircleCollider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer (작은 초록색 등 임시 스프라이트)
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ExperienceOrb : MonoBehaviour, IPoolable
    {
        [SerializeField] private float magnetRange = 5f;
        [SerializeField] private float magnetSpeed = 8f;

        private int expValue;
        private Transform attractTarget;
        private bool isAttracted;
        private bool isCollected;

        public void Initialize(Vector2 position, int exp)
        {
            transform.position = position;
            expValue = exp;
            isAttracted = false;
            isCollected = false;
            attractTarget = null;
        }

        private void Update()
        {
            if (isCollected) return;

            if (isAttracted && attractTarget != null)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    attractTarget.position,
                    magnetSpeed * Time.deltaTime);
                return;
            }

            Transform closest = FindClosestPlayer();
            if (closest != null)
            {
                float dist = Vector2.Distance(transform.position, closest.position);
                if (dist <= magnetRange)
                {
                    isAttracted = true;
                    attractTarget = closest;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;
            if (!other.CompareTag("Player")) return;

            isCollected = true;

            // 호스트에서만 경험치 처리
            if (PhotonNetwork.IsMasterClient)
            {
                GameManager.Instance?.AddExp(expValue);
                Debug.Log($"[ExperienceOrb] 획득! +{expValue} EXP");
            }

            PoolManager.Instance?.Return(gameObject);
        }

        private Transform FindClosestPlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            foreach (var p in players)
            {
                float dist = Vector2.Distance(transform.position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p.transform;
                }
            }

            return closest;
        }

        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            isCollected = false;
        }

        public void OnReturnToPool()
        {
            isAttracted = false;
            isCollected = false;
            attractTarget = null;
            gameObject.SetActive(false);
        }
    }
}
