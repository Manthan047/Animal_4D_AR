using UnityEngine;

public class Stego : MonoBehaviour, IAnimalAction
{
    [Header("Data Config")]
    [SerializeField] private AnimalData animalData;

    [Header("Animator Controls")]
    [SerializeField] private string actionTriggerName = "doaction";
    [SerializeField] private string audioTriggerName = "doaudio";
    [SerializeField] private string defaultStateName = "Blend Tree";
    [SerializeField] private int defaultStateLayer = 0;

    private Animator animator;
    private AudioSource audioSource;

    public string AnimalName => animalData != null ? animalData.animalName : "Stegosaurus";
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

        // This runs every time the target is (re)detected, not just once,
        // so it must be the place we force the default state and clear
        // any stale triggers left armed from before the target was lost.
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
        if (animator == null)
        {
            return;
        }

        // Clear any triggers that may still be armed from a previous
        // detection cycle, before they get a chance to fire.
        animator.ResetTrigger(actionTriggerName);
        animator.ResetTrigger(audioTriggerName);

        animator.enabled = true;
        animator.SetLayerWeight(defaultStateLayer, 1f);
        if (animator.HasState(defaultStateLayer, Animator.StringToHash(defaultStateName)))
        {
            animator.Play(defaultStateName, defaultStateLayer, 0f);
        }

        // Force immediate evaluation so the state actually lands on
        // Blend Tree before anything else (e.g. a manager callback on
        // this same frame) has a chance to set a trigger again.
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

            Debug.Log($"[Stego] Explicitly triggered {actionTriggerName} on layer 0 (Weight: {animator.GetLayerWeight(0)})");
        }
        else
        {
            Debug.LogError("[Stego] Animator reference is null!");
        }
    }

    public void PlayAudioWithAnimation()
    {
        Debug.Log("[Stego] PlayAudioWithAnimation method invoked!");
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