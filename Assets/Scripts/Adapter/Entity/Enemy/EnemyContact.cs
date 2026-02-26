using UnityEngine;
using Photon.Pun;
using SwDreams.Testing;

namespace SwDreams.Adapter.Entity
{
    /// <summary>
    /// 적의 접촉 데미지 처리.
    /// 호스트에서만 데미지 판정.
    /// 
    /// 주의: 이 컴포넌트가 붙은 Collider2D는 isTrigger = true 여야 함.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyContact : MonoBehaviour
    {
        [SerializeField] private float damageCooldown = 0.5f;

        private Enemy enemy;
        private float lastDamageTime;

        public void Initialize(Enemy enemyRef)
        {
            enemy = enemyRef;
            lastDamageTime = -damageCooldown;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (enemy == null || !enemy.IsAlive) return;
            if (Time.time - lastDamageTime < damageCooldown) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerStub>();
                if (player != null && player.IsAlive)
                {
                    player.TakeDamage(enemy.ContactDamage);
                    lastDamageTime = Time.time;
                    Debug.Log($"[EnemyContact] → Player에게 {enemy.ContactDamage} 데미지");
                }
            }
        }
    }
}
