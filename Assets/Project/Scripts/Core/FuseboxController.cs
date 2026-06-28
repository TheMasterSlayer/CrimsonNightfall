using UnityEngine;

[DisallowMultipleComponent]
public class FuseboxController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 35f;

    [Header("Required Inventory Ids")]
    [SerializeField] private string wrenchId = "Wrench";
    [SerializeField] private string fuse1Id = "Fuse_1";
    [SerializeField] private string fuse2Id = "Fuse_2";

    [Header("Fuse Lights")]
    [SerializeField] private Light fuse1Light;
    [SerializeField] private Light fuse2Light;
    [SerializeField] private Color missingFuseColor = Color.red;
    [SerializeField] private Color insertedFuseColor = Color.green;
    [SerializeField] private float lightIntensity = 8f;

    [Header("Messages")]
    [SerializeField] private string needsWrenchMessage = "It looks like I need to find an item to open this up.";
    [SerializeField] private string selectWrenchMessage = "Select the Wrench in your inventory first.";
    [SerializeField] private string needsFusesMessage = "It seems I need two fuses to power it on.";
    [SerializeField] private string selectFuseMessage = "Select a fuse in your inventory first.";
    [SerializeField] private string powerOnMessage = "The elevator power is back on.";

    [Header("Audio")]
    [SerializeField] private AudioClip wrenchUsedSound;
    [SerializeField] private AudioClip fuseInsertedSound;
    [SerializeField] private AudioClip poweredOnSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    [Header("Powered Buzz")]
    [SerializeField] private AudioClip poweredBuzzSound;
    [SerializeField] [Range(0f, 1f)] private float poweredBuzzVolume = 0.35f;

    private bool _isOpen;
    private bool _fuse1Inserted;
    private bool _fuse2Inserted;
    private bool _playedPoweredOnSound;
    private AudioSource _oneShotSource;
    private AudioSource _poweredBuzzSource;

    public static bool ElevatorPowerOn { get; private set; }

    private void Awake()
    {
        if (fuse1Light == null)
            fuse1Light = FindChildLight("Fuse1_Light");

        if (fuse2Light == null)
            fuse2Light = FindChildLight("Fuse2_Light");

        ElevatorPowerOn = false;
        ApplyLightState();
        PreloadAudio();
        EnsureOneShotSource();
        EnsurePoweredBuzzSource();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && IsFocused())
            Interact();
    }

    private void Interact()
    {
        if (!_isOpen)
        {
            if (!CollectionInventory.HasItem(wrenchId))
            {
                CollectionInventory.ShowBottomMessage(needsWrenchMessage);
                return;
            }

            if (!CollectionInventory.IsSelected(wrenchId))
            {
                CollectionInventory.ShowBottomMessage(selectWrenchMessage);
                return;
            }

            _isOpen = true;
            PlayOneShot(wrenchUsedSound);
            CollectionInventory.ShowBottomMessage(needsFusesMessage);
            return;
        }

        bool insertedAnyFuse = false;

        if (!_fuse1Inserted && CollectionInventory.IsSelected(fuse1Id))
        {
            _fuse1Inserted = true;
            insertedAnyFuse = true;
            CollectionInventory.ConsumeItem(fuse1Id);
            PlayOneShot(fuseInsertedSound);
            CollectionInventory.ShowBottomMessage("Fuse 1 was inserted.");
        }

        if (!_fuse2Inserted && CollectionInventory.IsSelected(fuse2Id))
        {
            _fuse2Inserted = true;
            insertedAnyFuse = true;
            CollectionInventory.ConsumeItem(fuse2Id);
            PlayOneShot(fuseInsertedSound);
            CollectionInventory.ShowBottomMessage("Fuse 2 was inserted.");
        }

        ApplyLightState();

        if (_fuse1Inserted && _fuse2Inserted)
        {
            ElevatorPowerOn = true;
            if (!_playedPoweredOnSound)
            {
                _playedPoweredOnSound = true;
                PlayOneShot(poweredOnSound);
                StartPoweredBuzz();
            }

            CollectionInventory.ShowBottomMessage(powerOnMessage);
            return;
        }

        if (!insertedAnyFuse)
        {
            bool hasUninsertedFuse = (!_fuse1Inserted && CollectionInventory.HasItem(fuse1Id)) ||
                                     (!_fuse2Inserted && CollectionInventory.HasItem(fuse2Id));
            CollectionInventory.ShowBottomMessage(hasUninsertedFuse ? selectFuseMessage : needsFusesMessage);
        }
    }

    private void ApplyLightState()
    {
        ApplyLight(fuse1Light, _fuse1Inserted);
        ApplyLight(fuse2Light, _fuse2Inserted);
    }

    private void ApplyLight(Light targetLight, bool inserted)
    {
        if (targetLight == null)
            return;

        targetLight.enabled = true;
        targetLight.color = inserted ? insertedFuseColor : missingFuseColor;
        targetLight.intensity = lightIntensity;
    }

    private Light FindChildLight(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Light>() : null;
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 toFusebox = GetFocusPoint() - camera.transform.position;
        if (toFusebox.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toFusebox) <= interactionAngle;
    }

    private Vector3 GetFocusPoint()
    {
        Renderer renderer = GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.center : transform.position;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureOneShotSource();
        if (_oneShotSource != null)
            _oneShotSource.PlayOneShot(clip, audioVolume);
    }

    private void EnsureOneShotSource()
    {
        if (_oneShotSource != null)
            return;

        _oneShotSource = gameObject.AddComponent<AudioSource>();
        _oneShotSource.playOnAwake = false;
        _oneShotSource.loop = false;
        _oneShotSource.spatialBlend = 1f;
        _oneShotSource.volume = audioVolume;
    }

    private void EnsurePoweredBuzzSource()
    {
        if (_poweredBuzzSource != null || poweredBuzzSound == null)
            return;

        _poweredBuzzSource = gameObject.AddComponent<AudioSource>();
        _poweredBuzzSource.playOnAwake = false;
        _poweredBuzzSource.loop = true;
        _poweredBuzzSource.spatialBlend = 1f;
        _poweredBuzzSource.clip = poweredBuzzSound;
        _poweredBuzzSource.volume = poweredBuzzVolume;
    }

    private void PreloadAudio()
    {
        PreloadClip(wrenchUsedSound);
        PreloadClip(fuseInsertedSound);
        PreloadClip(poweredOnSound);
        PreloadClip(poweredBuzzSound);
    }

    private static void PreloadClip(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    private void StartPoweredBuzz()
    {
        if (poweredBuzzSound == null)
            return;

        EnsurePoweredBuzzSource();
        if (_poweredBuzzSource == null)
            return;

        _poweredBuzzSource.clip = poweredBuzzSound;
        _poweredBuzzSource.volume = poweredBuzzVolume;
        _poweredBuzzSource.loop = true;

        if (!_poweredBuzzSource.isPlaying)
            _poweredBuzzSource.Play();
    }
}
