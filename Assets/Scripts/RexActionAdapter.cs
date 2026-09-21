using System.Collections;
using UnityEngine;

public class RexActionAdapter : MonoBehaviour, IAnimalAction
{
    [SerializeField] private AnimalData animalData;
    private Animator animator;
    private AudioSource audioSource;
    private Coroutine attackRoutine;

    public string AnimalName => animalData != null ? animalData.animalName : "Tyrannosaurus Rex";
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
        Debug.Log("RexActionAdapter: PlayActionAnimation called on " + gameObject.name);

        if (animator != null)
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(TriggerAttackRoutine());
        }
        else
        {
            Debug.LogWarning("RexActionAdapter: Animator is NULL on " + gameObject.name);
        }
    }

    public void PlayAudioWithAnimation()
    {
        Debug.Log("RexActionAdapter: PlayAudioWithAnimation called on " + gameObject.name);

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
        if (animator != null)
        {
            animator.SetBool("Attack", false);
            animator.SetInteger("Move", 0);
        }
    }

    public void PlayMove()
    {
        if (animator != null)
        {
            animator.SetInteger("Move", 1); // Move = 1 is walk
        }
    }

    public AnimalData GetAnimalData()
    {
        return animalData;
    }

    private IEnumerator TriggerAttackRoutine()
    {
        // Rex animator Attack parameter is a bool
        animator.SetBool("Attack", true);
        yield return new WaitForSeconds(1.5f);
        animator.SetBool("Attack", false);
        attackRoutine = null;
    }
}