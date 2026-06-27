using System.Collections.Generic;
using UnityEngine;
using System;

[DisallowMultipleComponent]
public class LaserBarrier : MonoBehaviour
{
    private static readonly List<LaserBarrier> Lasers = new List<LaserBarrier>();
    public static event Action LasersDisabled;
    public static bool AreLasersDisabled { get; private set; }

    [SerializeField] private bool startsEnabled = true;
    [SerializeField] private bool disableWholeObject = true;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private bool _disabled;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        SetLaserEnabled(startsEnabled);
    }

    private void OnEnable()
    {
        if (!Lasers.Contains(this))
            Lasers.Add(this);
    }

    private void OnDisable()
    {
        Lasers.Remove(this);
    }

    public static void DisableAllLasers()
    {
        AreLasersDisabled = true;

        foreach (LaserBarrier laser in Lasers.ToArray())
        {
            if (laser != null)
                laser.DisableLaser();
        }

        LasersDisabled?.Invoke();
    }

    public void DisableLaser()
    {
        if (_disabled)
            return;

        _disabled = true;
        SetLaserEnabled(false);

        if (disableWholeObject)
            gameObject.SetActive(false);
    }

    private void SetLaserEnabled(bool enabled)
    {
        if (_renderers != null)
        {
            foreach (Renderer laserRenderer in _renderers)
            {
                if (laserRenderer != null)
                    laserRenderer.enabled = enabled;
            }
        }

        if (_colliders != null)
        {
            foreach (Collider laserCollider in _colliders)
            {
                if (laserCollider != null)
                    laserCollider.enabled = enabled;
            }
        }
    }
}
