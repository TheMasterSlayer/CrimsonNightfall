using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SCP096PropPusher : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string propRootName = "SCP_Props";
    [SerializeField] private float impactRadius = 2.25f;
    [SerializeField] private float impactForwardOffset = 1.1f;
    [SerializeField] private bool onlyPushWhileChasing = true;
    [SerializeField] private float propHitCooldown = 0.08f;

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
    private NavMeshAgent _agent;
    private Transform _propRoot;

    private void Awake()
    {
        _controller = GetComponent<SCP096Controller>();
        _agent = GetComponent<NavMeshAgent>();
        _scpColliders = GetComponentsInChildren<Collider>(true);
        CachePropRoot();
    }

    private void Update()
    {
        if (onlyPushWhileChasing && (_controller == null || !_controller.IsChasing))
            return;

        if (_propRoot == null)
            CachePropRoot();

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

    private bool IsUnderPropRoot(Transform target)
    {
        if (_propRoot == null)
            return true;

        while (target != null)
        {
            if (target == _propRoot)
                return true;

            target = target.parent;
        }

        return false;
    }

    private SCPPropPhysicsObject GetMainPropForCollider(Collider hit)
    {
        if (hit == null)
            return null;

        if (_propRoot == null)
            return hit.GetComponentInParent<SCPPropPhysicsObject>();

        Transform current = hit.transform;
        Transform directChild = null;
        while (current != null)
        {
            if (current.parent == _propRoot)
            {
                directChild = current;
                break;
            }

            current = current.parent;
        }

        return directChild != null ? directChild.GetComponent<SCPPropPhysicsObject>() : null;
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

    private void CachePropRoot()
    {
        GameObject root = GameObject.Find(propRootName);
        _propRoot = root != null ? root.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + transform.forward * impactForwardOffset, impactRadius);
    }
}
