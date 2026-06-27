using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public class LaserLeverController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] [Range(5f, 90f)] private float interactionAngle = 35f;
    [SerializeField] private string promptMessage = "Press E to pull the lever.";
    [SerializeField] private string pulledMessage = "The lasers power down.";

    [Header("Animation")]
    [SerializeField] private Animator leverAnimator;
    [SerializeField] private AnimationClip pullClip;
    [SerializeField] private string fallbackStateName = "Take 001";

    [Header("Audio")]
    [SerializeField] private AudioClip leverPulledSound;
    [SerializeField] [Range(0f, 1f)] private float audioVolume = 1f;

    private bool _pulled;
    private bool _isAnimating;
    private PlayableGraph _graph;

    private void Awake()
    {
        if (leverAnimator == null)
            leverAnimator = GetComponentInChildren<Animator>(true);

        if (leverAnimator == null)
            leverAnimator = gameObject.AddComponent<Animator>();

        leverAnimator.applyRootMotion = false;
    }

    private void OnDisable()
    {
        if (_graph.IsValid())
            _graph.Destroy();
    }

    private void Update()
    {
        if (_pulled || _isAnimating)
            return;

        if (IsFocused())
        {
            CollectionInventory.ShowBottomMessage(promptMessage, 0.15f);

            if (Input.GetKeyDown(KeyCode.E))
                StartCoroutine(PullLever());
        }
    }

    private IEnumerator PullLever()
    {
        _isAnimating = true;
        PlayOneShot(leverPulledSound);
        float duration = PlayPullAnimation();

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        _pulled = true;
        _isAnimating = false;

        LaserBarrier.DisableAllLasers();
        CollectionInventory.ShowBottomMessage(pulledMessage, 2.5f);
    }

    private float PlayPullAnimation()
    {
        if (leverAnimator == null)
            return 0f;

        if (pullClip != null)
        {
            if (_graph.IsValid())
                _graph.Destroy();

            _graph = PlayableGraph.Create($"{name}_LeverPullGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_graph, pullClip);
            clipPlayable.SetApplyFootIK(false);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "LeverPull", leverAnimator);
            output.SetSourcePlayable(clipPlayable);
            _graph.Play();
            return pullClip.length;
        }

        if (leverAnimator.runtimeAnimatorController == null)
            return 0f;

        leverAnimator.Play(fallbackStateName, 0, 0f);
        return 1f;
    }

    private bool IsFocused()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return false;

        Vector3 focusPoint = GetFocusPoint();
        Vector3 toLever = focusPoint - camera.transform.position;
        if (toLever.magnitude > interactionRange)
            return false;

        return Vector3.Angle(camera.transform.forward, toLever) <= interactionAngle;
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
