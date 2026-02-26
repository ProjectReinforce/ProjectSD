using System.Collections.Generic;
using UnityEngine;
using SwDreams.Domain.Interfaces;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 범용 오브젝트 풀 매니저.
    /// 적, 투사체, 경험치 오브, 이펙트 등 IPoolable을 구현한 모든 것을 풀링.
    /// 
    /// 테스트 방법:
    /// 1. GameScene에 빈 GameObject → PoolManager 부착
    /// 2. 아무 프리팹(SpriteRenderer만 있어도 OK)에 PoolTestDummy 부착
    /// 3. PoolTestRunner에서 Get/Return 확인
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private Dictionary<GameObject, Queue<GameObject>> pools = new();
        private Dictionary<GameObject, Transform> poolParents = new();
        private Dictionary<GameObject, GameObject> instanceToPrefab = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 특정 프리팹에 대해 미리 풀을 채움.
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            EnsurePoolExists(prefab);

            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateNewInstance(prefab);
                obj.SetActive(false);
                pools[prefab].Enqueue(obj);
            }

            Debug.Log($"[PoolManager] Prewarm: {prefab.name} x{count}");
        }

        /// <summary>
        /// 풀에서 오브젝트를 꺼냄. 없으면 새로 생성.
        /// </summary>
        public GameObject Get(GameObject prefab)
        {
            EnsurePoolExists(prefab);

            GameObject obj;
            if (pools[prefab].Count > 0)
            {
                obj = pools[prefab].Dequeue();
            }
            else
            {
                obj = CreateNewInstance(prefab);
            }

            obj.GetComponent<IPoolable>()?.OnSpawnFromPool();
            return obj;
        }

        /// <summary>
        /// 오브젝트를 풀에 반환.
        /// </summary>
        public void Return(GameObject obj)
        {
            if (!instanceToPrefab.TryGetValue(obj, out GameObject prefab))
            {
                Debug.LogWarning($"[PoolManager] 풀에 등록되지 않은 오브젝트: {obj.name}");
                Destroy(obj);
                return;
            }

            obj.GetComponent<IPoolable>()?.OnReturnToPool();
            pools[prefab].Enqueue(obj);
        }

        /// <summary>
        /// 특정 프리팹 풀의 현재 대기 수.
        /// </summary>
        public int GetPoolCount(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out var queue))
                return queue.Count;
            return 0;
        }

        private void EnsurePoolExists(GameObject prefab)
        {
            if (!pools.ContainsKey(prefab))
            {
                pools[prefab] = new Queue<GameObject>();

                Transform parent = new GameObject($"Pool_{prefab.name}").transform;
                parent.SetParent(transform);
                poolParents[prefab] = parent;
            }
        }

        private GameObject CreateNewInstance(GameObject prefab)
        {
            Transform parent = poolParents[prefab];
            GameObject obj = Instantiate(prefab, parent);
            instanceToPrefab[obj] = prefab;
            return obj;
        }
    }
}
