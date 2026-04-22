using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Pickup.Adapter
{
    /// <summary>
    /// 모든 월드 픽업 아이템의 베이스. ExperienceOrb 의 자석/GameState 체크 로직을 일반화.
    ///
    /// 파생 클래스는 <see cref="OnPickedUpByPlayer"/> 템플릿 메서드만 구현.
    /// 호스트 권위 판정은 이 베이스에서 일괄 처리.
    ///
    /// 프리팹 구성 (파생 클래스 공통):
    /// - &lt;파생 스크립트&gt; (PickupItemBase 상속)
    /// - Collider2D (isTrigger = true)
    /// - Rigidbody2D (Kinematic)
    /// - SpriteRenderer
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class PickupItemBase : MonoBehaviour, IPoolable, IPickup
    {
        [SerializeField] protected string itemId;
        [SerializeField] protected PickupType type;
        [SerializeField] protected Rarity rarity = Rarity.Common;

        public string ItemId => itemId;
        public PickupType Type => type;
        public Rarity Rarity => rarity;

        private Transform attractTarget;
        private bool isAttracted;
        private bool isCollected;

        protected float MagnetRange =>
            GameManager.Instance?.Config != null ? GameManager.Instance.Config.magnetRange : 0.8f;
        protected float MagnetSpeed =>
            GameManager.Instance?.Config != null ? GameManager.Instance.Config.magnetSpeed : 1.3f;

        /// <summary>
        /// 풀에서 꺼낸 뒤 월드에 배치. 파생 클래스가 추가 초기화가 필요하면 override.
        /// </summary>
        public virtual void Initialize(Vector2 position)
        {
            transform.position = position;
            isAttracted = false;
            isCollected = false;
            attractTarget = null;
        }

        /// <summary>
        /// 자석(Magnet) 발동 시 외부에서 호출. 거리 무관 즉시 target 으로 끌어당김.
        /// 이미 획득된 상태면 no-op.
        /// </summary>
        public void ForceAttractTo(Transform target)
        {
            if (isCollected || target == null) return;
            isAttracted = true;
            attractTarget = target;
        }

        // Unity 매직 메서드는 private non-virtual 권장.
        // protected virtual 로 선언 시 파생이 override 안 하면 Unity 리플렉션이 Update 를 찾지 못해
        // 호출이 누락되는 이슈가 있음. 베이스에서 단일 책임 처리.
        private void Update()
        {
            if (isCollected) return;

            // 일시정지/비전투 상태에서 이동/흡수 중단
            var state = GameManager.Instance?.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight)
                return;

            if (isAttracted && attractTarget != null)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    attractTarget.position,
                    MagnetSpeed * Time.deltaTime);
                return;
            }

            Transform closest = FindClosestPlayer();
            if (closest != null)
            {
                float dist = Vector2.Distance(transform.position, closest.position);
                if (dist <= MagnetRange)
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

            // 호스트 권위 픽업 판정. 파생 클래스 훅에서 필요 시 RPC 로 전파.
            if (PhotonNetwork.IsMasterClient)
                OnPickedUpByPlayer(other.gameObject);

            PoolManager.Instance?.Return(gameObject);
        }

        /// <summary>
        /// 호스트에서만 호출. 파생 클래스가 실제 획득 로직 구현.
        /// 획득자 Player GameObject 가 전달됨 — 필요 시 GetComponent/PhotonView 로 소유자 조회.
        /// </summary>
        protected abstract void OnPickedUpByPlayer(GameObject playerObj);

        private Transform FindClosestPlayer()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return null;

            Transform closest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < players.Length; i++)
            {
                float dist = Vector2.Distance(transform.position, players[i].transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = players[i].transform;
                }
            }

            return closest;
        }

        public virtual void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
            isCollected = false;
        }

        public virtual void OnReturnToPool()
        {
            isAttracted = false;
            isCollected = false;
            attractTarget = null;
            gameObject.SetActive(false);
        }
    }
}
