using UnityEngine;
using JSukoAnimals;

public class StegosaurusActionAdapter : MonoBehaviour, IAnimalAction
{
    [SerializeField] private AnimalData animalData;
    private StegosaurusCharacter stegosaurusCharacter;
    private AudioSource audioSource;
    private Animator animator;

    public string AnimalName => animalData != null ? animalData.animalName : "Stegosaurus";
    public bool IsTracked { get; set; } = false;

    void Start()
    {
        stegosaurusCharacter = GetComponent<StegosaurusCharacter>();
        audioSource = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        if (animator != null)
        {
            animator.SetBool("IsGrounded", true);
            animator.SetBool("IsLived", true);
        }
    }

    void Update()
    {
        // Force grounded & alive state every frame for AR stability
        if (animator != null)
        {
            animator.SetBool("IsGrounded", true);
            animator.SetBool("IsLived", true);
        }
    }

    public void PlayActionAnimation()
    {
        Debug.Log("StegosaurusActionAdapter: PlayActionAnimation called on " + gameObject.name);
        if (stegosaurusCharacter != null)
        {
            stegosaurusCharacter.Attack();
        }
        else if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    public void PlayAudioWithAnimation()
    {
        if (audioSource != null)
        {
            if (animalData != null && animalData.roarClip != null)
            {
                audioSource.clip = animalData.roarClip;
            }
            audioSource.Stop();
            audioSource.Play();
        }

        PlayActionAnimation();
    }

    public void PlayIdle()
    {
        if (stegosaurusCharacter != null)
        {
            stegosaurusCharacter.forwardSpeed = 0;
            stegosaurusCharacter.turnSpeed = 0;
        }
        if (animator != null)
        {
            animator.SetFloat("Forward", 0);
            animator.SetFloat("Turn", 0);
        }
    }

    public void PlayMove()
    {
        if (stegosaurusCharacter != null)
        {
            stegosaurusCharacter.Walk();
        }
        else if (animator != null)
        {
            animator.SetFloat("Forward", 1f);
        }
    }

    public AnimalData GetAnimalData()
    {
        return animalData;
    }
}