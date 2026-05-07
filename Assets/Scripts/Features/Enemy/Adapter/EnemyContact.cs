using UnityEngine;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Enemy.Adapter;
using Photon.Pun;
using SwDreams.Shared.Managers;
using SwDreams.Testing;

namespace SwDreams.Features.Enemy.Adapter
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

            // B6: 레벨업/메뉴 등 일시정지 상태에선 접촉 데미지 발생 금지.
            var gm = GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.BossFight)
                return;

            if (Time.time - lastDamageTime < damageCooldown) return;

            if (other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerStub>();
                if (player != null && player.IsAlive)
                {
                    // B-1a: 가해 적 ID 동봉 — 자기 사망 시 LastDamagerEnemyId 기록 진입점.
                    player.TakeDamageFromEnemy(enemy.ContactDamage, enemy.EnemyId);
                    lastDamageTime = Time.time;
                    Debug.Log($"[EnemyContact] → Player에게 {enemy.ContactDamage} 데미지");
                }
            }
        }
    }
}
