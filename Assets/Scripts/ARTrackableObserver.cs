using UnityEngine;
using Vuforia;

public class ARTrackableObserver : MonoBehaviour
{
    private ObserverBehaviour observer;
    private IAnimalAction animalAction;

    private void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
    }

    private void Start()
    {
        animalAction = GetComponentInChildren<IAnimalAction>(true);

        if (observer != null)
        {
            observer.OnTargetStatusChanged += HandleTargetStatusChanged;
        }
    }

    private void OnDestroy()
    {
        if (observer != null)
        {
            observer.OnTargetStatusChanged -= HandleTargetStatusChanged;
        }
    }

    private void HandleTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        if (animalAction == null)
        {
            animalAction = GetComponentInChildren<IAnimalAction>(true);
        }

        bool isTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (isTracked)
        {
            OnTargetFound();
        }
        else
        {
            OnTargetLost();
        }
    }

    private void OnTargetFound()
    {
        Debug.Log($"[ARTrackableObserver] Target Found: {gameObject.name}");

        if (animalAction != null)
        {
            animalAction.IsTracked = true;
            animalAction.PlayIdle();

            if (ARAnimalManager.Instance != null)
            {
                ARAnimalManager.Instance.RegisterTrackedAnimal(animalAction);
            }
        }
    }

    private void OnTargetLost()
    {
        Debug.Log($"[ARTrackableObserver] Target Lost: {gameObject.name}");

        if (animalAction != null)
        {
            animalAction.IsTracked = false;

            if (ARAnimalManager.Instance != null)
            {
                ARAnimalManager.Instance.UnregisterTrackedAnimal(animalAction);
            }
        }
    }
}
