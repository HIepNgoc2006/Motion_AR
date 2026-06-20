using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionAR.Core
{
    /// <summary>
    /// Generic object pool that pre-allocates and recycles GameObjects to
    /// avoid runtime instantiation overhead and reduce garbage collection.
    /// <para>
    /// Supports multiple pool categories (audio sources, UI elements, particles)
    /// with configurable initial sizes and max capacities.
    /// </para>
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        [Serializable]
        public class PoolConfig
        {
            [Tooltip("Unique tag to identify this pool (e.g., 'AudioSource', 'DamageText').")]
            public string tag;

            [Tooltip("Prefab to instantiate for this pool.")]
            public GameObject prefab;

            [Tooltip("Number of objects to pre-allocate on startup.")]
            public int initialSize = 10;

            [Tooltip("Maximum pool capacity. 0 = unlimited growth.")]
            public int maxSize = 50;

            [Tooltip("Parent transform for pooled objects (null = this transform).")]
            public Transform parent;
        }

        [SerializeField]
        [Tooltip("List of all pool configurations.")]
        private List<PoolConfig> _poolConfigs = new List<PoolConfig>();

        /// <summary>Global singleton instance.</summary>
        public static ObjectPooler Instance { get; private set; }

        private readonly Dictionary<string, Queue<GameObject>> _pools =
            new Dictionary<string, Queue<GameObject>>();

        private readonly Dictionary<string, PoolConfig> _configLookup =
            new Dictionary<string, PoolConfig>();

        private readonly Dictionary<string, int> _activeCount =
            new Dictionary<string, int>();

        /// <summary>
        /// Tracks currently active (in-use) GameObjects per pool tag.
        /// Used by <see cref="ReturnAll"/> to collect and return all active objects.
        /// </summary>
        private readonly Dictionary<string, HashSet<GameObject>> _activeObjects =
            new Dictionary<string, HashSet<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializePools();
        }

        /// <summary>
        /// Pre-allocates all configured pools.
        /// </summary>
        private void InitializePools()
        {
            foreach (var config in _poolConfigs)
            {
                if (string.IsNullOrEmpty(config.tag) || config.prefab == null)
                {
                    Debug.LogWarning($"[ObjectPooler] Skipping invalid pool config: tag='{config.tag}'");
                    continue;
                }

                if (_pools.ContainsKey(config.tag))
                {
                    Debug.LogWarning($"[ObjectPooler] Duplicate pool tag: '{config.tag}'");
                    continue;
                }

                var queue = new Queue<GameObject>(config.initialSize);
                Transform parent = config.parent != null ? config.parent : transform;

                for (int i = 0; i < config.initialSize; i++)
                {
                    GameObject obj = CreatePooledObject(config.prefab, parent);
                    queue.Enqueue(obj);
                }

                _pools[config.tag] = queue;
                _configLookup[config.tag] = config;
                _activeCount[config.tag] = 0;
                _activeObjects[config.tag] = new HashSet<GameObject>();

                Debug.Log($"[ObjectPooler] Pool '{config.tag}' initialized with {config.initialSize} objects.");
            }
        }

        /// <summary>
        /// Retrieves an object from the pool. If the pool is empty and max capacity
        /// hasn't been reached, a new object is instantiated.
        /// </summary>
        /// <param name="tag">The pool tag to retrieve from.</param>
        /// <param name="position">World position to place the object.</param>
        /// <param name="rotation">World rotation for the object.</param>
        /// <returns>The retrieved or newly created GameObject, or null if pool is at max capacity.</returns>
        public GameObject Get(string tag, Vector3 position, Quaternion rotation)
        {
            if (!_pools.TryGetValue(tag, out var queue))
            {
                Debug.LogWarning($"[ObjectPooler] Pool not found: '{tag}'");
                return null;
            }

            GameObject obj = null;

            if (queue.Count > 0)
            {
                obj = queue.Dequeue();

                // Skip destroyed objects.
                while (obj == null && queue.Count > 0)
                {
                    obj = queue.Dequeue();
                }
            }

            if (obj == null)
            {
                // Pool empty — create new if under max capacity.
                var config = _configLookup[tag];
                int totalCount = _activeCount[tag] + queue.Count;

                if (config.maxSize > 0 && totalCount >= config.maxSize)
                {
                    Debug.LogWarning($"[ObjectPooler] Pool '{tag}' at max capacity ({config.maxSize}).");
                    return null;
                }

                Transform parent = config.parent != null ? config.parent : transform;
                obj = CreatePooledObject(config.prefab, parent);
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            _activeCount[tag]++;
            _activeObjects[tag].Add(obj);
            return obj;
        }

        /// <summary>
        /// Retrieves an object at default position/rotation.
        /// </summary>
        public GameObject Get(string tag)
        {
            return Get(tag, Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Returns an object to its pool. The object is deactivated.
        /// </summary>
        /// <param name="tag">The pool tag this object belongs to.</param>
        /// <param name="obj">The GameObject to return.</param>
        public void Return(string tag, GameObject obj)
        {
            if (obj == null) return;

            if (!_pools.TryGetValue(tag, out var queue))
            {
                Debug.LogWarning($"[ObjectPooler] Pool not found for return: '{tag}'. Destroying object.");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            queue.Enqueue(obj);

            if (_activeObjects.TryGetValue(tag, out var activeSet))
            {
                activeSet.Remove(obj);
            }

            if (_activeCount.ContainsKey(tag) && _activeCount[tag] > 0)
            {
                _activeCount[tag]--;
            }
        }

        /// <summary>
        /// Returns an object to the pool after a delay (in seconds).
        /// Useful for particle effects, damage numbers, etc.
        /// </summary>
        public async Cysharp.Threading.Tasks.UniTaskVoid ReturnDelayed(string tag, GameObject obj, float delay)
        {
            if (obj == null) return;

            await Cysharp.Threading.Tasks.UniTask.Delay(
                TimeSpan.FromSeconds(delay),
                ignoreTimeScale: false,
                Cysharp.Threading.Tasks.PlayerLoopTiming.Update,
                destroyCancellationToken
            );

            Return(tag, obj);
        }

        /// <summary>
        /// Returns all currently active objects of a specific pool back to the pool.
        /// Each object is deactivated and re-enqueued.
        /// </summary>
        /// <param name="tag">Pool tag to return all objects for.</param>
        public void ReturnAll(string tag)
        {
            if (!_activeObjects.TryGetValue(tag, out var activeSet) || activeSet.Count == 0)
            {
                return;
            }

            if (!_pools.TryGetValue(tag, out var queue))
            {
                return;
            }

            // Copy to array to avoid modifying the set while iterating.
            var snapshot = new GameObject[activeSet.Count];
            activeSet.CopyTo(snapshot);

            foreach (var obj in snapshot)
            {
                if (obj == null) continue;

                obj.SetActive(false);
                queue.Enqueue(obj);
            }

            activeSet.Clear();
            _activeCount[tag] = 0;
        }

        /// <summary>
        /// Gets pool statistics for debugging.
        /// </summary>
        /// <param name="tag">Pool tag.</param>
        /// <param name="available">Number of available (inactive) objects.</param>
        /// <param name="active">Number of currently active objects.</param>
        /// <returns>True if the pool exists.</returns>
        public bool GetPoolStats(string tag, out int available, out int active)
        {
            if (_pools.TryGetValue(tag, out var queue))
            {
                available = queue.Count;
                active = _activeCount.GetValueOrDefault(tag, 0);
                return true;
            }

            available = 0;
            active = 0;
            return false;
        }

        /// <summary>
        /// Registers a new pool at runtime (useful for dynamic prefab loading).
        /// </summary>
        /// <param name="tag">Pool identifier.</param>
        /// <param name="prefab">Prefab to instantiate.</param>
        /// <param name="initialSize">Initial pre-allocation count.</param>
        /// <param name="maxSize">Maximum capacity (0 = unlimited).</param>
        public void RegisterPool(string tag, GameObject prefab, int initialSize = 5, int maxSize = 0)
        {
            if (_pools.ContainsKey(tag))
            {
                Debug.LogWarning($"[ObjectPooler] Pool '{tag}' already exists.");
                return;
            }

            var config = new PoolConfig
            {
                tag = tag,
                prefab = prefab,
                initialSize = initialSize,
                maxSize = maxSize,
                parent = transform
            };

            var queue = new Queue<GameObject>(initialSize);
            for (int i = 0; i < initialSize; i++)
            {
                queue.Enqueue(CreatePooledObject(prefab, transform));
            }

            _pools[tag] = queue;
            _configLookup[tag] = config;
            _activeCount[tag] = 0;
            _activeObjects[tag] = new HashSet<GameObject>();
        }

        // ─────────────────── Private ───────────────────

        private GameObject CreatePooledObject(GameObject prefab, Transform parent)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            return obj;
        }
    }
}
