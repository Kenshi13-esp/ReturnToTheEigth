using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReturnToTheEigth.TimeTravel
{
    /// <summary>Checks inactive destination Collider2D shapes in an isolated, non-simulated 2D world.</summary>
    internal sealed class TimelineCollisionWorld : IDisposable
    {
        private const string SceneNamePrefix = "TimelineClearance2D_";
        private const string ProxyRootName = "QueryOnlyColliders2D";
        private const string UnsupportedWarning = "Travel blocked: unsupported Collider2D type: ";
        private const int NoHits = 0;
        private const int OneHit = 1;
        private const int LayerBit = 1;
        private readonly Scene queryScene;
        private readonly PhysicsScene2D physicsScene;
        private readonly Transform proxyRoot;
        private readonly Dictionary<Collider2D, Collider2D> proxies = new Dictionary<Collider2D, Collider2D>();
        private readonly List<Collider2D> destinationColliders = new List<Collider2D>();
        private readonly Collider2D[] overlaps = new Collider2D[OneHit];

        /// <summary>Creates a query world that cannot interact with the live player.</summary>
        public TimelineCollisionWorld()
        {
            queryScene = SceneManager.CreateScene(SceneNamePrefix + Guid.NewGuid(),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physicsScene = queryScene.GetPhysicsScene2D();
            GameObject root = new GameObject(ProxyRootName);
            SceneManager.MoveGameObjectToScene(root, queryScene);
            proxyRoot = root.transform;
        }

        /// <summary>Checks the player's oriented footprint against destination solids without activating its root.</summary>
        public bool IsClear(GameObject destinationRoot, Vector2 center, Vector2 size, float angle, LayerMask obstacleLayers)
        {
            foreach (Collider2D proxy in proxies.Values)
            {
                if (proxy != null)
                {
                    proxy.enabled = false;
                }
            }
            destinationColliders.Clear();
            destinationRoot.GetComponentsInChildren(true, destinationColliders);
            foreach (Collider2D source in destinationColliders)
            {
                if (source.isTrigger || (obstacleLayers.value & (LayerBit << source.gameObject.layer)) == NoHits
                    || !IsActiveBelowRoot(source.transform, destinationRoot.transform)
                    || (source.attachedRigidbody != null && !source.attachedRigidbody.simulated))
                {
                    continue;
                }
                ITimelineObstacle obstacle = source.GetComponentInParent<ITimelineObstacle>(true);
                if (!(obstacle != null ? obstacle.WillBlockTimeline(source) : source.enabled))
                {
                    continue;
                }
                if (!proxies.TryGetValue(source, out Collider2D proxy) || proxy == null)
                {
                    proxy = CreateProxy(source);
                    if (proxy == null)
                    {
                        Debug.LogWarning(UnsupportedWarning + source.GetType().Name, source);
                        return false;
                    }
                    proxies[source] = proxy;
                }
                CopyShape(source, proxy);
                proxy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                proxy.transform.localScale = source.transform.lossyScale;
                proxy.gameObject.layer = source.gameObject.layer;
                proxy.enabled = true;
            }
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleLayers);
            return physicsScene.OverlapBox(center, size, angle, filter, overlaps) == NoHits;
        }

        /// <summary>Unloads the query world and its collider copies when the manager is destroyed.</summary>
        public void Dispose()
        {
            if (queryScene.IsValid() && queryScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(queryScene);
            }
            proxies.Clear();
        }

        private Collider2D CreateProxy(Collider2D source)
        {
            GameObject proxyObject = new GameObject(source.name);
            SceneManager.MoveGameObjectToScene(proxyObject, queryScene);
            proxyObject.transform.SetParent(proxyRoot, false);
            Collider2D proxy;
            if (source is BoxCollider2D) proxy = proxyObject.AddComponent<BoxCollider2D>();
            else if (source is CircleCollider2D) proxy = proxyObject.AddComponent<CircleCollider2D>();
            else if (source is CapsuleCollider2D) proxy = proxyObject.AddComponent<CapsuleCollider2D>();
            else if (source is EdgeCollider2D) proxy = proxyObject.AddComponent<EdgeCollider2D>();
            else if (source is PolygonCollider2D) proxy = proxyObject.AddComponent<PolygonCollider2D>();
            else
            {
                UnityEngine.Object.Destroy(proxyObject);
                return null;
            }
            proxy.enabled = false;
            return proxy;
        }

        private static void CopyShape(Collider2D source, Collider2D proxy)
        {
            proxy.offset = source.offset;
            if (source is BoxCollider2D box && proxy is BoxCollider2D targetBox)
            {
                targetBox.size = box.size;
                targetBox.edgeRadius = box.edgeRadius;
            }
            else if (source is CircleCollider2D circle && proxy is CircleCollider2D targetCircle)
                targetCircle.radius = circle.radius;
            else if (source is CapsuleCollider2D capsule && proxy is CapsuleCollider2D targetCapsule)
            {
                targetCapsule.size = capsule.size;
                targetCapsule.direction = capsule.direction;
            }
            else if (source is EdgeCollider2D edge && proxy is EdgeCollider2D targetEdge)
            {
                targetEdge.points = edge.points;
                targetEdge.edgeRadius = edge.edgeRadius;
            }
            else if (source is PolygonCollider2D polygon && proxy is PolygonCollider2D targetPolygon)
            {
                targetPolygon.pathCount = polygon.pathCount;
                for (int index = NoHits; index < polygon.pathCount; index++)
                    targetPolygon.SetPath(index, polygon.GetPath(index));
            }
        }

        private static bool IsActiveBelowRoot(Transform candidate, Transform root)
        {
            for (Transform current = candidate; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
    }
}
