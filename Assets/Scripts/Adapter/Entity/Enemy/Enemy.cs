using System;
using UnityEngine;
using SwDreams.Domain.Interfaces;
using SwDreams.Application;
using SwDreams.Data;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적 엔티티. 상태(HP) 관리 + 이벤트 발행.
    /// 이동은 EnemyMovement, 접촉 판정은 EnemyContact에서 처리.
    /// 
    /// PhotonView 없음. 네트워크 동기화는 SpawnManager가 RPC로 처리.
    /// 각 적은 고유 ID로 호스트-클라이언트 간 매칭.
    /// </summary>
    [RequireComponent(typeof(EnemyMovement))]
    [RequireComponent(typeof(EnemyContact))]
    public class Enemy : MonoBehaviour, IDamageable, IPoolable
    {
        // 네트워크 식별용
        public int EnemyId { get; private set; }

        // 데이터
        private EnemyData enemyData;
        private DamageService damageService;

        // 상태
        public int CurrentHP { get; private set; }
        public int MaxHP { get; private set; }
        public bool IsAlive => CurrentHP > 0;
        public int ExpValue => enemyData != null ? enemyData.expValue : 0;
        public float MoveSpeed => enemyData != null ? enemyData.moveSpeed : 0f;
        public int ContactDamage => enemyData != null ? enemyData.contactDamage : 0;

        // 이벤트
        public event Action<int, int> OnHealthChanged;
        public event Action OnDied;
        public event Action<Enemy> OnDiedWithRef;

        // 컴포넌트 캐시
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// 스폰 시 초기화. SpawnManager에서 호출.
        /// </summary>
        public void Initialize(int id, EnemyData data, Vector2 position,
            DamageService dmgService, float hpMultiplier = 1f)
        {
            EnemyId = id;
            enemyData = data;
            damageService = dmgService;

            MaxHP = Mathf.RoundToInt(data.baseHP * hpMultiplier);
            CurrentHP = MaxHP;
            transform.position = position;
            gameObject.tag = "Enemy";

            if (spriteRenderer != null && data.sprite != null)
                spriteRenderer.sprite = data.sprite;

            GetComponent<EnemyMovement>().Initialize(this);
            GetComponent<EnemyContact>().Initialize(this);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;

            var result = damageService.ProcessSkillAttack(damage);
            CurrentHP = Mathf.Max(0, CurrentHP - result.FinalDamage);
            OnHealthChanged?.Invoke(CurrentHP, MaxHP);

            if (!IsAlive)
                Die();
        }

        private void Die()
        {
            OnDied?.Invoke();
            OnDiedWithRef?.Invoke(this);
        }

        // === IPoolable ===
        public void OnSpawnFromPool()
        {
            gameObject.SetActive(true);
        }

        public void OnReturnToPool()
        {
            OnDied = null;
            OnDiedWithRef = null;
            OnHealthChanged = null;
            CurrentHP = 0;
            EnemyId = -1;
            gameObject.SetActive(false);
        }
    }
}
