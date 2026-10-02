using UnityEngine;

public class RexAnimationController: MonoBehaviour, IAnimalAction
{
    [Header("Data Config")]
    [SerializeField] private AnimalData animalData;

    [Header("Animator Controls")]
    [SerializeField] private string actionTriggerName = "doaction";
    [SerializeField] private string audioTriggerName = "doaudio";
    [SerializeField] private string defaultStateName = "Rex|Idle1A";
    [SerializeField] private int defaultStateLayer = 0;

    private Animator animator;
    private AudioSource audioSource;

    public string AnimalName => animalData != null ? animalData.animalName : "Tyrannosaurus Rex";
    public bool IsTracked { get; set; } = false;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        RegisterWithManager();
    }

    private void OnEnable()
    {
        RegisterWithManager();

        // Runs every time this animal is (re)detected, not just once, so
        // it's the right place to force the default idle state and clear
        // any trigger left armed from before the target was lost.
        EnsureAnimatorReference();
        ResetToDefaultState();
    }

    private void OnDisable()
    {
        IsTracked = false;
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.UnregisterTrackedAnimal(this);
        }
    }

    private void RegisterWithManager()
    {
        IsTracked = true;
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.RegisterTrackedAnimal(this);
        }
    }

    private void ResetToDefaultState()
    {
        if (animator == null) return;

        animator.ResetTrigger(actionTriggerName);
        animator.ResetTrigger(audioTriggerName);

        animator.enabled = true;
        animator.SetLayerWeight(defaultStateLayer, 1f);
        if (animator.HasState(defaultStateLayer, Animator.StringToHash(defaultStateName)))
        {
            animator.Play(defaultStateName, defaultStateLayer, 0f);
        }
        animator.Update(0f);
    }

    public void PlayActionAnimation()
    {
        EnsureAnimatorReference();

        if (animator != null)
        {
            animator.enabled = true;
            animator.SetLayerWeight(0, 1f);

            animator.ResetTrigger(actionTriggerName);
            animator.SetTrigger(actionTriggerName);

            animator.Update(0f);

            Debug.Log($"[Rex] Explicitly triggered {actionTriggerName} on layer 0 (Weight: {animator.GetLayerWeight(0)})");
        }
        else
        {
            Debug.LogError("[Rex] Animator reference is null!");
        }
    }

    public void PlayAudioWithAnimation()
    {
        EnsureAnimatorReference();

        if (animator != null)
        {
            animator.ResetTrigger(audioTriggerName);
            animator.SetTrigger(audioTriggerName);
        }

        if (audioSource != null)
        {
            if (animalData != null && animalData.roarClip != null)
            {
                audioSource.clip = animalData.roarClip;
            }

            if (audioSource.clip != null)
            {
                audioSource.Stop();
                audioSource.Play();
            }
        }
    }

    public void PlayIdle()
    {
        EnsureAnimatorReference();
        if (animator != null)
        {
            animator.SetFloat("Blend", 0f);
        }
    }

    public void PlayMove()
    {
        EnsureAnimatorReference();
        if (animator != null)
        {
            animator.SetFloat("Blend", 1f);
        }
    }

    public AnimalData GetAnimalData()
    {
        return animalData;
    }

    private void EnsureAnimatorReference()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null && !animator.enabled)
        {
            animator.enabled = true;
        }
    }
}