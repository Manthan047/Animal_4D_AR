using System.Collections;
using UnityEngine;
using Vuforia;

public class ARTrackableObserver : MonoBehaviour
{
    // The card that currently owns the screen. Only one at a time.
    private static ARTrackableObserver current;

    [Header("Settings")]
    [Tooltip("Small delay before hiding, so a brief tracking drop does not flicker.")]
    [SerializeField] private float lostDelay = 0.25f;

    private ObserverBehaviour observer;
    private IAnimalAction animalAction;
    private Coroutine lostRoutine;
    private bool isFound;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        observer = GetComponent<ObserverBehaviour>();
    }

    private void Start()
    {
        animalAction = GetComponentInChildren<IAnimalAction>(true);

        if (observer == null) return;

        observer.OnTargetStatusChanged += HandleTargetStatusChanged;

        // Do not hide if the card is already tracked when the app starts
        if (observer.TargetStatus.Status == Status.TRACKED)
            OnTargetFound();
        else
            SetContentVisible(false);
    }

    private void OnDestroy()
    {
        if (observer != null)
            observer.OnTargetStatusChanged -= HandleTargetStatusChanged;

        if (current == this) current = null;
    }

    private void OnDisable()
    {
        if (isFound) ForceHide();
    }

    // =========================================================
    // VUFORIA STATUS
    // =========================================================

    private void HandleTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus status)
    {
        if (animalAction == null)
            animalAction = GetComponentInChildren<IAnimalAction>(true);

        Debug.Log($"[ARTrackableObserver] {gameObject.name} -> {status.Status}");

        // Only TRACKED counts. EXTENDED_TRACKED, LIMITED, NO_POSE = lost.
        if (status.Status == Status.TRACKED)
        {
            CancelPendingLost();
            OnTargetFound();
        }
        else if (isFound && lostRoutine == null)
        {
            lostRoutine = StartCoroutine(LostAfterDelay());
        }
    }

    private IEnumerator LostAfterDelay()
    {
        if (lostDelay > 0f) yield return new WaitForSeconds(lostDelay);

        lostRoutine = null;
        OnTargetLost();
    }

    private void CancelPendingLost()
    {
        if (lostRoutine != null)
        {
            StopCoroutine(lostRoutine);
            lostRoutine = null;
        }
    }

    // =========================================================
    // FOUND / LOST
    // =========================================================

    private void OnTargetFound()
    {
        if (isFound) return;
        isFound = true;

        Debug.Log($"[ARTrackableObserver] Target Found: {gameObject.name}");

        // Hide the previous card first (model, audio, registration)
        if (current != null && current != this)
            current.ForceHide();

        current = this;

        SetContentVisible(true);

        if (animalAction == null) return;

        // Set the flag BEFORE registering, so the UI and buttons see a tracked animal
        animalAction.IsTracked = true;

        if (ARAnimalManager.Instance != null)
            ARAnimalManager.Instance.RegisterTrackedAnimal(animalAction);

        animalAction.PlayIdle();
    }

    private void OnTargetLost()
    {
        if (!isFound) return;

        Debug.Log($"[ARTrackableObserver] Target Lost: {gameObject.name}");

        ForceHide();

        if (current == this) current = null;
    }

    // Hides this card and unregisters it. Also called by the next card.
    private void ForceHide()
    {
        CancelPendingLost();
        isFound = false;

        if (animalAction != null)
        {
            animalAction.IsTracked = false;

            if (ARAnimalManager.Instance != null)
                ARAnimalManager.Instance.UnregisterTrackedAnimal(animalAction);
        }

        SetContentVisible(false);
    }

    // =========================================================
    // VISIBILITY
    // Hides visuals only. The animal script stays ACTIVE, so its
    // coroutines, flags and Animator are not reset by a deactivate.
    // =========================================================

    private void SetContentVisible(bool visible)
    {
        if (visible)
        {
            // Make sure children are active (a model may start disabled in the scene)
            foreach (Transform child in transform)
            {
                if (!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
            }
        }
        else
        {
            foreach (AudioSource src in GetComponentsInChildren<AudioSource>(true))
                src.Stop();
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = visible;

        foreach (Canvas cv in GetComponentsInChildren<Canvas>(true))
            cv.enabled = visible;
    }
}