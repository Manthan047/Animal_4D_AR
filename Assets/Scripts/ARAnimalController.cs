using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ARAnimalController : MonoBehaviour, IAnimalAction
{
    [Header("Animal Metadata")]
    [SerializeField] private AnimalData animalData;
    [SerializeField] private string overrideName = "";

    [Header("Animation Trigger Names")]
    [SerializeField] private string actionTrigger = "DoAction";
    [SerializeField] private string audioTrigger = "DoAudio";
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string moveTrigger = "Move";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip actionSoundClip;


 


    private Animator animator;
    private HashSet<string> parameterNames = new HashSet<string>();
    private Dictionary<string, AnimatorControllerParameterType> parameterTypes = new Dictionary<string, AnimatorControllerParameterType>();
    private Coroutine activeActionRoutine;

    public string AnimalName => !string.IsNullOrEmpty(overrideName) ? overrideName : (animalData != null ? animalData.animalName : gameObject.name);
    public bool IsTracked { get; set; } = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        CacheAnimatorParameters();
    }

    private void Start()
    {
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        // Apply scale/offset if specified in AnimalData
        if (animalData != null && animalData.targetScale != Vector3.zero)
        {
            // Only apply if targetScale is not default 0
            transform.localScale = animalData.targetScale;
        }
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null) return;

        parameterNames.Clear();
        parameterTypes.Clear();

        foreach (var param in animator.parameters)
        {
            parameterNames.Add(param.name);
            parameterTypes[param.name] = param.type;
        }
    }

    public void PlayActionAnimation()
    {
        Debug.Log($"[ARAnimalController] Triggering Action on {AnimalName}");
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
        }
        activeActionRoutine = StartCoroutine(PerformActionRoutine(actionTrigger));
    }

    public void PlayAudioWithAnimation()
    {
        Debug.Log($"[ARAnimalController] Triggering Audio + Animation on {AnimalName}");
        
        // Play Audio Clip
        PlayRoarAudio();

        // Play Animation
        if (activeActionRoutine != null)
        {
            StopCoroutine(activeActionRoutine);
        }

        // Try audioTrigger first, fallback to actionTrigger if audioTrigger not present
        string triggerToUse = parameterNames.Contains(audioTrigger) ? audioTrigger : actionTrigger;
        activeActionRoutine = StartCoroutine(PerformActionRoutine(triggerToUse));
    }

    public void PlayIdle()
    {
        if (animator == null) return;

        if (parameterNames.Contains(idleTrigger))
        {
            TriggerOrSetParam(idleTrigger, true);
        }
        else if (parameterNames.Contains("Blend"))
        {
            animator.SetFloat("Blend", 0f);
        }
    }

    public void PlayMove()
    {
        if (animator == null) return;

        if (parameterNames.Contains(moveTrigger))
        {
            TriggerOrSetParam(moveTrigger, true);
        }
        else if (parameterNames.Contains("Forward"))
        {
            animator.SetFloat("Forward", 1f);
        }
        else if (parameterNames.Contains("Blend"))
        {
            animator.SetFloat("Blend", 1f);
        }
    }

    public AnimalData GetAnimalData()
    {
        return animalData;
    }

    private void PlayRoarAudio()
    {
        AudioClip clipToPlay = actionSoundClip;
        if (clipToPlay == null && animalData != null && animalData.roarClip != null)
        {
            clipToPlay = animalData.roarClip;
        }

        if (audioSource != null)
        {
            if (clipToPlay != null)
            {
                audioSource.Stop();
                audioSource.clip = clipToPlay;
                audioSource.Play();
            }
            else if (audioSource.clip != null)
            {
                audioSource.Stop();
                audioSource.Play();
            }
        }
    }

    private IEnumerator PerformActionRoutine(string triggerName)
    {
        if (animator == null) yield break;

        // Fallback names if specified trigger is not found
        string targetParam = triggerName;
        if (!parameterNames.Contains(targetParam))
        {
            if (parameterNames.Contains("Attack")) targetParam = "Attack";
            else if (parameterNames.Contains("DoAction")) targetParam = "DoAction";
            else if (parameterNames.Contains("Action")) targetParam = "Action";
            else if (parameterNames.Contains("DoAudio")) targetParam = "DoAudio";
        }

        if (parameterNames.Contains(targetParam))
        {
            var paramType = parameterTypes[targetParam];
            if (paramType == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(targetParam);
                animator.SetTrigger(targetParam);
            }
            else if (paramType == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(targetParam, true);
                yield return new WaitForSeconds(1.5f); // Hold bool for animation start
                animator.SetBool(targetParam, false);
            }
            else if (paramType == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(targetParam, 1);
                yield return new WaitForSeconds(1.5f);
                animator.SetInteger(targetParam, 0);
            }
        }

        activeActionRoutine = null;
    }

    private void TriggerOrSetParam(string paramName, bool state)
    {
        if (animator == null || !parameterNames.Contains(paramName)) return;

        var paramType = parameterTypes[paramName];
        if (paramType == AnimatorControllerParameterType.Trigger)
        {
            if (state)
            {
                animator.ResetTrigger(paramName);
                animator.SetTrigger(paramName);
            }
        }
        else if (paramType == AnimatorControllerParameterType.Bool)
        {
            animator.SetBool(paramName, state);
        }
    }

   
}
