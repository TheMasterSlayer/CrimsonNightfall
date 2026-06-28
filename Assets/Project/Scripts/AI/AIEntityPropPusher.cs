using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class AIEntityPropPusher : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private string propsRootPrefix = "Props";
    [SerializeField] private string[] exactRootNames = { "Items", "Hiding_Area_1", "Hiding_Area_2" };
    [SerializeField] private bool autoAddPhysicsToMainChildren = true;

    [Header("Detection")]
    [SerializeField] private float impactRadius = 2.1f;
    [SerializeField] private float impactForwardOffset = 1.05f;
    [SerializeField] private bool onlyPushWhileChasing = true;
    [SerializeField] private float propHitCooldown = 0.08f;
    [SerializeField] [Min(0.01f)] private float physicsCheckInterval = 0.05f;

    [Header("Impact")]
    [SerializeField] private float baseImpactForce = 70f;
    [SerializeField] private float maxImpactForce = 190f;
    [SerializeField] private float upwardLift = 0.1f;
    [SerializeField] private float torqueForce = 28f;
    [SerializeField] private float minimumRunSpeedForFullForce = 3.5f;
    [SerializeField] private bool temporarilyIgnorePropCollision = true;
    [SerializeField] private float ignorePropCollisionDuration = 1f;

    private readonly Dictionary<SCPPropPhysicsObject, float> _nextHitTime = new Dictionary<SCPPropPhysicsObject, float>();
    private readonly List<Transform> _roots = new List<Transform>();
    private AIEntity _entity;
    private NavMeshAgent _agent;
    private Collider[] _entityColliders;
    private float _nextPhysicsCheckTime;

    private void Awake()
    {
        _entity = GetComponent<AIEntity>();
        _agent = GetComponent<NavMeshAgent>();
        _entityColliders = GetComponentsInChildren<Collider>(true);
        CacheRoots();

        if (autoAddPhysicsToMainChildren)
            PrepareMainChildren();
    }

    private void Update()
    {
        if (onlyPushWhileChasing && (_entity == null || !_entity.IsChasing))
            return;

        if (Time.time < _nextPhysicsCheckTime)
            return;

        _nextPhysicsCheckTime = Time.time + physicsCheckInterval;

        if (_roots.Count == 0)
        {
            CacheRoots();
            if (autoAddPhysicsToMainChildren)
                PrepareMainChildren();
        }

        Vector3 center = transform.position + transform.forward * impactForwardOffset;
        Collider[] hits = Physics.OverlapSphere(center, impactRadius, ~0, QueryTriggerInteraction.Ignore);
        Vector3 velocity = _agent != null ? _agent.velocity : transform.forward * minimumRunSpeedForFullForce;

        foreach (Collider hit in hits)
        {
            SCPPropPhysicsObject prop = GetMainPropForCollider(hit);
            if (prop == null || prop.transform == transform)
                continue;

            if (_nextHitTime.TryGetValue(prop, out float nextHitTime) && Time.time < nextHitTime)
                continue;

            _nextHitTime[prop] = Time.time + propHitCooldown;

            if (temporarilyIgnorePropCollision)
                StartCoroutine(TemporarilyIgnoreCollision(prop));

            prop.ApplyScpImpact(
                transform.position,
                velocity,
                baseImpactForce,
                maxImpactForce,
                torqueForce,
                upwardLift,
                minimumRunSpeedForFullForce);
        }
    }

    private void CacheRoots()
    {
        _roots.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
                continue;

            if (IsAllowedRootName(candidate.name))
                _roots.Add(candidate);
        }
    }

    private bool IsAllowedRootName(string rootName)
    {
        if (!string.IsNullOrWhiteSpace(propsRootPrefix) && rootName.StartsWith(propsRootPrefix))
            return true;

        if (exactRootNames == null)
            return false;

        foreach (string exactRootName in exactRootNames)
        {
            if (!string.IsNullOrWhiteSpace(exactRootName) && rootName == exactRootName)
                return true;
        }

        return false;
    }

    private void PrepareMainChildren()
    {
        foreach (Transform root in _roots)
        {
            if (root == null)
                continue;

            foreach (Transform child in root)
            {
                if (child == null)
                    continue;

                if (!HasPropSurface(child))
                    continue;

                SCPPropPhysicsObject prop = child.GetComponent<SCPPropPhysicsObject>();
                if (prop == null)
                    prop = child.gameObject.AddComponent<SCPPropPhysicsObject>();

                prop.RefreshPhysicsProfile();
            }
        }
    }

    private bool HasPropSurface(Transform target)
    {
        return target.GetComponentInChildren<Renderer>(true) != null ||
               target.GetComponentInChildren<Collider>(true) != null;
    }

    private SCPPropPhysicsObject GetMainPropForCollider(Collider hit)
    {
        if (hit == null)
            return null;

        foreach (Transform root in _roots)
        {
            Transform directChild = GetDirectChildUnderRoot(hit.transform, root);
            if (directChild != null)
                return directChild.GetComponent<SCPPropPhysicsObject>();
        }

        return null;
    }

    private Transform GetDirectChildUnderRoot(Transform target, Transform root)
    {
        if (target == null || root == null)
            return null;

        Transform current = target;
        while (current != null)
        {
            if (current.parent == root)
                return current;

            current = current.parent;
        }

        return null;
    }

    private IEnumerator TemporarilyIgnoreCollision(SCPPropPhysicsObject prop)
    {
        if (prop == null || _entityColliders == null)
            yield break;

        Collider[] propColliders = prop.GetComponentsInChildren<Collider>(true);
        SetCollisionIgnored(propColliders, true);
        yield return new WaitForSeconds(ignorePropCollisionDuration);
        SetCollisionIgnored(propColliders, false);
    }

    private void SetCollisionIgnored(Collider[] propColliders, bool ignored)
    {
        foreach (Collider entityCollider in _entityColliders)
        {
            if (entityCollider == null)
                continue;

            foreach (Collider propCollider in propColliders)
            {
                if (propCollider == null)
                    continue;

                Physics.IgnoreCollision(entityCollider, propCollider, ignored);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + transform.forward * impactForwardOffset, impactRadius);
    }
}
