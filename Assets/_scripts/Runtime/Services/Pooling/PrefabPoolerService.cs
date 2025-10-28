using System.Collections.Generic;
using UnityEngine;
using Yunus.Game.Core;
using Debug = UnityEngine.Debug;
using UObject = UnityEngine.Object;

namespace Yunus.Game.Services
{
    public sealed class PrefabPoolerService : IPrefabPooler
    {
        // --- Dahili havuz modeli ---
        public sealed class Pool
        {
            public GameObject Prefab;
            public readonly Queue<GameObject> Available = new();
            public readonly List<GameObject> InUse = new();
            public Transform Root;
        }

        private readonly Dictionary<GameObject, Pool> _pools = new();
        private readonly Dictionary<GameObject, Pool> _instanceToPool = new();
        private Transform _root;

        // ---- IService / ITickable ----
        public void Initialize()
        {
            _root = new GameObject("[PoolService]").transform;
            GameObject.DontDestroyOnLoad(_root.gameObject);
        }

        public void Tick() { }

        public void Clean()
        {
            foreach (var p in _pools.Values)
            {
                foreach (var go in p.Available) if (go) UObject.Destroy(go);
                foreach (var go in p.InUse) if (go) UObject.Destroy(go);
            }
            _pools.Clear();
            _instanceToPool.Clear();
            if (_root) UObject.Destroy(_root.gameObject);
        }

        // ---- API ----
        public IPrefabPool CreatePool(GameObject prefab, int prewarmCount = 0)
        {
            if (!prefab)
            {
                Debug.LogError("[Pooler] CreatePool: prefab is null");
                return null;
            }

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Pool
                {
                    Prefab = prefab,
                    Root = new GameObject($"Pool_{prefab.name}").transform
                };
                pool.Root.SetParent(_root, false);
                _pools[prefab] = pool;

                // prewarm
                for (int i = 0; i < prewarmCount; i++)
                {
                    var obj = NewInstance(pool);
                    obj.SetActive(false);
                    pool.Available.Enqueue(obj);
                }
            }
            else if (prewarmCount > 0 && pool.Available.Count < prewarmCount)
            {
                int need = prewarmCount - pool.Available.Count;
                for (int i = 0; i < need; i++)
                {
                    var obj = NewInstance(pool);
                    obj.SetActive(false);
                    pool.Available.Enqueue(obj);
                }
            }

            return new Handle(this, prefab);
        }

        // ---- Handle (single prefab pool view) ----
        private sealed class Handle : IPrefabPool
        {
            private readonly PrefabPoolerService svc;
            private readonly GameObject prefab;

            public Handle(PrefabPoolerService svc, GameObject prefab)
            {
                this.svc = svc;
                this.prefab = prefab;
            }

            public GameObject Spawn(Vector3 pos, Quaternion rot, Transform parent = null)
                => SpawnImmediate(pos, rot, parent);

            public GameObject SpawnImmediate(Vector3 pos, Quaternion rot, Transform parent = null)
            {
                if (!svc._pools.TryGetValue(prefab, out var pool))
                {
                    svc.CreatePool(prefab, 0);
                    pool = svc._pools[prefab];
                }

                var go = (pool.Available.Count > 0) ? pool.Available.Dequeue() : svc.NewInstance(pool);
                go.transform.SetParent(parent ? parent : pool.Root, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);

                pool.InUse.Add(go);
                svc._instanceToPool[go] = pool;
                return go;
            }

            public void Despawn(GameObject instance)
            {
                if (!instance) return;
                if (!svc._instanceToPool.TryGetValue(instance, out var pool))
                {
                    UObject.Destroy(instance); // Not from pool
                    return;
                }

                if (!pool.InUse.Remove(instance)) return;
                instance.SetActive(false);
                instance.transform.SetParent(pool.Root, false);
                pool.Available.Enqueue(instance);
                svc._instanceToPool.Remove(instance);
            }

            public void DespawnAll()
            {
                if (!svc._pools.TryGetValue(prefab, out var pool)) return;
                var copy = new List<GameObject>(pool.InUse);
                foreach (var go in copy) Despawn(go);
            }

            public (int available, int inUse, int total) Stats
            {
                get
                {
                    if (!svc._pools.TryGetValue(prefab, out var p)) return (0, 0, 0);
                    return (p.Available.Count, p.InUse.Count, p.Available.Count + p.InUse.Count);
                }
            }
        }

        // ---- internals ----
        // Instance method (fixes CS0176 error)
        private GameObject NewInstance(Pool p)
        {
            var go = UObject.Instantiate(p.Prefab, p.Root);
            go.name = p.Prefab.name + "_pooled";
            go.SetActive(false);
            return go;
        }
    }
}
