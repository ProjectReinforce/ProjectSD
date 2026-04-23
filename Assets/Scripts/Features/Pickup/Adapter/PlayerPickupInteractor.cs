using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

namespace SwDreams.Features.Pickup.Adapter
{
    /// <summary>
    /// 플레이어의 상호작용 픽업 추적기.
    /// Player 프리팹의 자식 GameObject 에 부착 — Player 본체 Collider 가 PickupItemBase 의
    /// 트리거에 닿으면 Enter/Exit 콜백으로 Register/Unregister 호출됨.
    ///
    /// Space 키 감지 → 가장 가까운 "획득 가능한" 픽업에 TryInteract.
    ///
    /// 로컬 플레이어 전용. 원격 플레이어 프리팹에서는 비활성.
    /// </summary>
    public class PlayerPickupInteractor : MonoBehaviour
    {
        private readonly HashSet<PickupItemBase> nearby = new HashSet<PickupItemBase>();

        private PhotonView ownerView;

        /// <summary>현재 프레임의 "가장 가까운 상호작용 대상". 없으면 null.</summary>
        public PickupItemBase CurrentTarget { get; private set; }

        /// <summary>CurrentTarget 이 지금 획득 가능한지 (false 면 UI 회색 처리).</summary>
        public bool CurrentTargetPickupable { get; private set; }

        /// <summary>CurrentTarget 또는 그 가능 여부가 바뀌면 발생 (UI 바인딩용).</summary>
        public event Action OnTargetChanged;

        private void Awake()
        {
            ownerView = GetComponentInParent<PhotonView>();
        }

        private void Update()
        {
            // 로컬 플레이어만 상호작용 처리 (멀티플레이에서 원격 플레이어가 자기 쪽 키 입력으로 픽업하게 하지 않음)
            if (ownerView != null && !ownerView.IsMine) return;

            PruneDead();
            UpdateCurrentTarget();
            HandleInput();
        }

        // ===== 근접 등록 / 해제 (PickupItemBase 가 호출) =====

        public void RegisterNearby(PickupItemBase pickup)
        {
            if (pickup == null) return;
            nearby.Add(pickup);
        }

        public void UnregisterNearby(PickupItemBase pickup)
        {
            if (pickup == null) return;
            nearby.Remove(pickup);
        }

        // ===== 내부 로직 =====

        /// <summary>풀 반환되거나 파괴된 엔트리 정리.</summary>
        private void PruneDead()
        {
            if (nearby.Count == 0) return;
            nearby.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
        }

        private void UpdateCurrentTarget()
        {
            PickupItemBase closest = null;
            float minDist = float.MaxValue;
            Vector3 myPos = transform.position;

            foreach (var p in nearby)
            {
                if (p == null) continue;
                float d = Vector2.SqrMagnitude((Vector2)(p.transform.position - myPos));
                if (d < minDist)
                {
                    minDist = d;
                    closest = p;
                }
            }

            bool pickupable = closest != null && closest.CanBePickedUpBy(gameObject);

            if (closest != CurrentTarget || pickupable != CurrentTargetPickupable)
            {
                CurrentTarget = closest;
                CurrentTargetPickupable = pickupable;
                OnTargetChanged?.Invoke();
            }
        }

        private void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!kb.spaceKey.wasPressedThisFrame) return;
            if (CurrentTarget == null) return;
            if (!CurrentTargetPickupable) return;

            CurrentTarget.TryInteract(gameObject);
            // 성공 시 PickupItemBase 가 풀 반환 — 다음 PruneDead 에서 nearby 에서 제거됨.
        }
    }
}
