using UnityEngine;
using Vuforia;

public class PlayIdleOnTracking : MonoBehaviour
{
    private ObserverBehaviour observer;
    private IAnimalAction animalAction;
    private Animator animator;

    void Start()
    {
        observer = GetComponent<ObserverBehaviour>();
        animalAction = GetComponentInChildren<IAnimalAction>(true);
        animator = GetComponentInChildren<Animator>(true);

        if (observer != null)
        {
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }
        else
        {
            Debug.LogWarning(gameObject.name + ": No ObserverBehaviour found. Attach this script to the ImageTarget GameObject.");
        }
    }

    void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        bool isTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (isTracked)
        {
            if (animalAction != null)
            {
                animalAction.PlayIdle();
            }
            else if (animator != null && animator.HasState(0, Animator.StringToHash("Idle")))
            {
                animator.Play("Idle", 0, 0f);
            }
        }
    }

    void OnDestroy()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        }
    }
}