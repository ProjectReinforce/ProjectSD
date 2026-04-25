using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Pickup.Domain;
using SwDreams.Features.Character.Adapter;
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

            // 상호작용 픽업(Essence/Weapon)은 자석 흡수 대상 아님 — Space 로만 획득.
            // (단, 외부에서 ForceAttractTo 로 강제 흡수 발동 시에는 동작 — 자석 아이템용.
            //  자석은 현재 ExpOrb 만 타겟팅하므로 정수/무기가 끌려올 일 없음.)
            if (RequiresInteraction && !isAttracted) return;

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

        /// <summary>
        /// true 면 접촉 즉시 획득하지 않고, 플레이어가 직접 상호작용 키(Space)를 눌러야 획득.
        /// Essence/Weapon 등 전략적 획득이 필요한 픽업은 override 로 true 반환.
        /// PlayerPickupInteractor 가 트리거 Enter/Exit 를 감지해 상호작용 프롬프트를 관리.
        /// </summary>
        public virtual bool RequiresInteraction => false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;
            if (!other.CompareTag("Player")) return;

            if (RequiresInteraction)
            {
                // 상호작용 방식: 즉시 획득 대신 InteractionPrompt 등록.
                var interactor = other.GetComponentInChildren<PlayerPickupInteractor>();
                interactor?.RegisterNearby(this);
                return;
            }

            // 기존 즉시 획득 경로 (ExpOrb/Magnet/Potion)
            if (!CanBePickedUpBy(other.gameObject)) return;

            isCollected = true;

            if (PhotonNetwork.IsMasterClient)
                OnPickedUpByPlayer(other.gameObject);

            PoolManager.Instance?.Return(gameObject);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!RequiresInteraction) return;
            if (!other.CompareTag("Player")) return;

            var interactor = other.GetComponentInChildren<PlayerPickupInteractor>();
            interactor?.UnregisterNearby(this);
        }

        /// <summary>
        /// 상호작용 키 입력으로 호출되는 획득 시도. PlayerPickupInteractor 가 호출.
        /// 2슬롯 꽉 참 등의 이유로 차단되면 false 반환 (월드에 그대로 남음).
        /// 호스트 권위 — 클라는 호스트에 RPC 위임하고 호스트가 OnPickedUpByPlayer 처리.
        /// </summary>
        public bool TryInteract(GameObject playerObj)
        {
            if (isCollected) return false;
            if (!RequiresInteraction) return false;
            if (!CanBePickedUpBy(playerObj)) return false;

            if (PhotonNetwork.IsMasterClient)
            {
                ProcessPickupAsHost(playerObj);
            }
            else
            {
                // 클라: 호스트에 위치+itemId 로 위임. 호스트가 자기 측 인스턴스 찾아 처리.
                // playerObj 는 PlayerPickupInteractor 가 부착된 자식일 수 있어 부모까지 탐색.
                var stub = playerObj.GetComponentInParent<PlayerStub>();
                if (stub == null)
                {
                    Debug.LogWarning($"[PickupItemBase] PlayerStub 미발견 — playerObj='{playerObj.name}'. 픽업 위임 실패.");
                    return false;
                }
                stub.RequestPickupFromClient(transform.position, itemId);
            }
            return true;
        }

        /// <summary>
        /// 호스트가 직접 호출하거나, 클라 RPC 위임을 받아 호스트가 호출.
        /// isCollected 갱신 + OnPickedUpByPlayer + 풀 반환을 일괄 수행.
        /// 다른 플레이어/풀이 동시에 처리하는 race 는 isCollected 가드로 방어.
        /// </summary>
        public void ProcessPickupAsHost(GameObject playerObj)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (isCollected) return;
            if (!RequiresInteraction) return;
            if (!CanBePickedUpBy(playerObj)) return;

            isCollected = true;
            OnPickedUpByPlayer(playerObj);

            // 다른 클라들에 풀 반환 알림 — 호스트만 자기 풀 정리하면 클라 측에 stale 인스턴스가 남아
            // 다음 클라 픽업 시도가 호스트에서 매칭 실패함.
            DropSpawner.Instance?.NotifyPickupCollected((Vector2)transform.position, itemId);

            PoolManager.Instance?.Return(gameObject);
        }

        /// <summary>
        /// 실제 획득 가능 여부. false 반환 시 접촉해도 풀 반환되지 않고 월드에 남는다.
        /// 기본 true. 예: EssencePickup 이 2슬롯 꽉 찬 경우 false 반환.
        /// RequiresInteraction 픽업에서는 프롬프트 회색 상태 결정에도 사용.
        /// </summary>
        public virtual bool CanBePickedUpBy(GameObject playerObj) => true;

        /// <summary>
        /// 상호작용 프롬프트에 표시할 액션 라벨. 기본 "획득".
        /// Weapon 등이 "조합" 같은 다른 라벨을 쓸 때 override.
        /// </summary>
        public virtual string PromptActionLabel => "획득";

        /// <summary>
        /// 상호작용 프롬프트 부가 정보 (예: 무기 조합 결과). 기본 null.
        /// </summary>
        public virtual string PromptExtraInfo => null;

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
