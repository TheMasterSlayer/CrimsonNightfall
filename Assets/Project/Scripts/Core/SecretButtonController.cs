using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SecretButtonController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 35f;
    [SerializeField] private string activatedMessage = "You have enabled a secret room.";

    [Header("Secret Passage")]
    [SerializeField] private GameObject secretDoor;
    [SerializeField] private GameObject[] additionalPassageBlockers;
    [SerializeField] private bool blockPassageUntilPressed = true;
    [SerializeField] private bool keepDoorInvisible = true;

    [Header("Glow")]
    [SerializeField] private Light passageGlow;
    [SerializeField] private Color glowColor = new Color(0.35f, 0.85f, 1f);
    [SerializeField] private float glowIntensity = 1.35f;
    [SerializeField] private float glowRange = 3f;
    [SerializeField] private Vector3 glowLocalOffset = new Vector3(0f, 0.25f, -0.35f);

    [Header("Audio")]
    [SerializeField] private AudioClip buttonPressedSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private bool _activated;
    private Collider[] _secretDoorColliders;
    private Renderer[] _secretDoorRenderers;
    private Collider[] _additionalBlockerColliders;

    private void Awake()
    {
        AutoFindReferences();
        CacheSecretDoorParts();
        CacheAdditionalBlockers();
        ConfigureInitialPassageState();
    }

    private void Update()
    {
        if (_activated)
            return;

        if (!IsFocused())
            return;

        if (Input.GetKeyDown(KeyCode.E))
            ActivateSecretPassage();
    }

    private void AutoFindReferences()
    {
        if (secretDoor == null)
        {
            GameObject foundDoor = GameObject.Find("Secret_Door");
            if (foundDoor != null)
                secretDoor = foundDoor;
        }

        if (additionalPassageBlockers == null || additionalPassageBlockers.Length == 0)
        {
            List<GameObject> blockers = new List<GameObject>();
            AddFoundObject(blockers, "Secret_Wall_1");
            AddFoundObject(blockers, "Secret_Wall_2");
            additionalPassageBlockers = blockers.ToArray();
        }
    }

    private void CacheSecretDoorParts()
    {
        if (secretDoor == null)
            return;

        _secretDoorColliders = secretDoor.GetComponentsInChildren<Collider>(true);
        _secretDoorRenderers = secretDoor.GetComponentsInChildren<Renderer>(true);

        if (passageGlow == null)
        {
            Transform existingGlow = secretDoor.transform.Find("SecretDoor_BlueGlow");
            if (existingGlow != null)
                passageGlow = existingGlow.GetComponent<Light>();
        }

        if (passageGlow == null)
            passageGlow = CreatePassageGlow();
    }

    private void CacheAdditionalBlockers()
    {
        if (additionalPassageBlockers == null || additionalPassageBlockers.Length == 0)
        {
            _additionalBlockerColliders = System.Array.Empty<Collider>();
            return;
        }

        List<Collider> colliders = new List<Collider>();
        foreach (GameObject blocker in additionalPassageBlockers)
        {
            if (blocker == null)
                continue;

            colliders.AddRange(blocker.GetComponentsInChildren<Collider>(true));
        }

        _additionalBlockerColliders = colliders.ToArray();
    }

    private void ConfigureInitialPassageState()
    {
        SetDoorVisible(!keepDoorInvisible);
        SetDoorCollidersEnabled(blockPassageUntilPressed);
        SetAdditionalBlockerCollidersEnabled(blockPassageUntilPressed);

        if (passageGlow != null)
            passageGlow.enabled = false;
    }

    private void ActivateSecretPassage()
    {
        _activated = true;
        PlayOneShot(buttonPressedSound);

        SetDoorVisible(!keepDoorInvisible);
        SetDoorCollidersEnabled(false);
        SetAdditionalBlockerCollidersEnabled(false);

        if (passageGlow != null)
        {
            passageGlow.color = glowColor;
            passageGlow.intensity = glowIntensity;
            passageGlow.range = glowRange;
            passageGlow.enabled = true;
        }

        CollectionInventory.ShowBottomMessage(activatedMessage, 3f);
    }

    private static void AddFoundObject(List<GameObject> objects, string objectName)
    {
        GameObject foundObject = GameObject.Find(objectName);
        if (foundObject != null)
            objects.Add(foundObject);
    }

    private Light CreatePassageGlow()
    {
        if (secretDoor == null)
            return null;

        GameObject glowObject = new GameObject("SecretDoor_BlueGlow");
        glowObject.transform.SetParent(secretDoor.transform, false);
        glowObject.transform.localPosition = glowLocalOffset;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one;

        Light light = glowObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = glowColor;
        light.intensity = glowIntensity;
        light.range = glowRange;
        light.enabled = false;
        return light;
    }

    private void SetDoorVisible(bool visible)
    {
        if (_secretDoorRenderers == null)
            return;

        foreach (Renderer doorRenderer in _secretDoorRenderers)
        {
            if (doorRenderer != null)
                doorRenderer.enabled = visible;
        }
    }

    private void SetDoorCollidersEnabled(bool enabled)
    {
        if (_secretDoorColliders == null)
            return;

        foreach (Collider doorCollider in _secretDoorColliders)
        {
            if (doorCollider != null)
                doorCollider.enabled = enabled;
        }
    }

    private void SetAdditionalBlockerCollidersEnabled(bool enabled)
    {
        if (_additionalBlockerColliders == null)
            return;

        foreach (Collider blockerCollider in _additionalBlockerColliders)
        {
            if (blockerCollider != null)
                blockerCollider.enabled = enabled;
        }
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 toButton = focusPoint - camera.transform.position;
        if (toButton.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toButton) <= interactionAngle;
    }

    private Vector3 GetFocusPoint()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, GetFocusPoint(), audioVolume);
    }
}
