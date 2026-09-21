using UnityEngine;

public class AnimalControllerUI : MonoBehaviour, IAnimalAction
{
    [SerializeField] private AnimalData animalData;
    private Animator animator;
    private AudioSource audioSource;

    public string AnimalName => animalData != null ? animalData.animalName : gameObject.name;
    public bool IsTracked { get; set; } = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }
    }

    public void PlayActionAnimation()
    {
        if (animator != null)
        {
            animator.ResetTrigger("DoAction");
            animator.SetTrigger("DoAction");
        }
    }

    public void PlayAudioWithAnimation()
    {
        if (animator != null)
        {
            animator.ResetTrigger("DoAudio");
            animator.SetTrigger("DoAudio");
        }

        if (audioSource != null)
        {
            if (animalData != null && animalData.roarClip != null)
            {
                audioSource.clip = animalData.roarClip;
            }
            audioSource.Stop();
            audioSource.Play();
        }
    }

    public void PlayIdle()
    {
        if (animator != null)
        {
            animator.SetFloat("Blend", 0f);
        }
    }

    public void PlayMove()
    {
        if (animator != null)
        {
            animator.SetFloat("Blend", 1f);
        }
    }

    public AnimalData GetAnimalData()
    {
        return animalData;
    }
}