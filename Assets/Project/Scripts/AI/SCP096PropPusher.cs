using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SCP096PropPusher : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private string propsRootPrefix = "Props";
    [SerializeField] private string[] exactRootNames = { "SCP_Props", "Items", "Hiding_Area_1", "Hiding_Area_2" };
    [SerializeField] private bool autoAddPhysicsToMainChildren = true;

    [Header("Detection")]
    [SerializeField] private float impactRadius = 2.25f;
    [SerializeField] private float impactForwardOffset = 1.1f;
    [SerializeField] private bool onlyPushWhileChasing = true;
    [SerializeField] private float propHitCooldown = 0.08f;
    [SerializeField] [Min(0.01f)] private float physicsCheckInterval = 0.05f;

    [Header("Impact")]
    [SerializeField] private float baseImpactForce = 85f;
    [SerializeField] private float maxImpactForce = 220f;
    [SerializeField] private float upwardLift = 0.12f;
    [SerializeField] private float torqueForce = 32f;
    [SerializeField] private float minimumRunSpeedForFullForce = 4f;
    [SerializeField] private bool temporarilyIgnorePropCollision = true;
    [SerializeField] private float ignorePropCollisionDuration = 1.25f;

    private readonly Dictionary<SCPPropPhysicsObject, float> _nextHitTime = new Dictionary<SCPPropPhysicsObject, float>();
    private Collider[] _scpColliders;
    private SCP096Controller _controller;
    private SCP096MansionEnemy _mansionEnemy;
    private NavMeshAgent _agent;
    private readonly List<Transform> _roots = new List<Transform>();
    private float _nextPhysicsCheckTime;

    private void Awake()
    {
        _controller = GetComponent<SCP096Controller>();
        _mansionEnemy = GetComponent<SCP096MansionEnemy>();
        _agent = GetComponent<NavMeshAgent>();
        _scpColliders = GetComponentsInChildren<Collider>(true);
        CacheRoots();

        if (autoAddPhysicsToMainChildren)
            PrepareMainChildren();
    }

    private void Update()
    {
        if (onlyPushWhileChasing && !IsChasing())
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

    private bool IsChasing()
    {
        if (_controller != null && _controller.IsChasing)
            return true;

        return _mansionEnemy != null && _mansionEnemy.IsChasing;
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
        if (prop == null || _scpColliders == null)
            yield break;

        Collider[] propColliders = prop.GetComponentsInChildren<Collider>(true);
        SetCollisionIgnored(propColliders, true);
        yield return new WaitForSeconds(ignorePropCollisionDuration);
        SetCollisionIgnored(propColliders, false);
    }

    private void SetCollisionIgnored(Collider[] propColliders, bool ignored)
    {
        foreach (Collider scpCollider in _scpColliders)
        {
            if (scpCollider == null)
                continue;

            foreach (Collider propCollider in propColliders)
            {
                if (propCollider == null)
                    continue;

                Physics.IgnoreCollision(scpCollider, propCollider, ignored);
            }
        }
    }

    private void CacheRoots()
    {
        _roots.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null && IsAllowedRootName(candidate.name))
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
                if (child == null || !HasPropSurface(child))
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + transform.forward * impactForwardOffset, impactRadius);
    }
}
