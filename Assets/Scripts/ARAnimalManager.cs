using System;
using System.Collections.Generic;
using UnityEngine;

public class ARAnimalManager : MonoBehaviour
{
    public static ARAnimalManager Instance { get; private set; }

    public event Action<IAnimalAction> OnAnimalTracked;
    public event Action<IAnimalAction> OnAnimalLost;
    public event Action<IAnimalAction> OnSelectedAnimalChanged;

    private readonly List<IAnimalAction> activeTrackedAnimals = new List<IAnimalAction>();

    [Header("Selection Settings")]
    [Tooltip("If true, a newly detected animal automatically becomes selected.")]
    [SerializeField] private bool autoSelectNewlyDetected = true;

    [Tooltip("If true, only ONE animal can be active at a time. " +
             "A newly detected animal hides the previous one (model, buttons, audio).")]
    [SerializeField] private bool singleActiveAnimal = true;

    private IAnimalAction selectedAnimal;

    public IReadOnlyList<IAnimalAction> ActiveTrackedAnimals => activeTrackedAnimals;
    public IAnimalAction SelectedAnimal => selectedAnimal;
    public bool HasActiveAnimal => activeTrackedAnimals.Count > 0;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        selectedAnimal = null;
    }

    // =========================================================
    // ANIMAL TRACKED
    // =========================================================

    public void RegisterTrackedAnimal(IAnimalAction animal)
    {
        if (animal == null) return;
        if (activeTrackedAnimals.Contains(animal)) return;

        // FIX: hide every previously active animal first
        if (singleActiveAnimal)
        {
            // copy the list because we modify it while looping
            var previous = new List<IAnimalAction>(activeTrackedAnimals);

            foreach (IAnimalAction other in previous)
            {
                if (other == null) continue;

                other.IsTracked = false;   // hides model, buttons, stops audio
                activeTrackedAnimals.Remove(other);
                OnAnimalLost?.Invoke(other);
            }
        }

        activeTrackedAnimals.Add(animal);

        Debug.Log($"[ARAnimalManager] Registered: {animal.AnimalName}. " +
                  $"Total tracked: {activeTrackedAnimals.Count}");

        OnAnimalTracked?.Invoke(animal);

        if (selectedAnimal == null || autoSelectNewlyDetected || singleActiveAnimal)
        {
            SelectAnimal(animal);
        }
    }

    // =========================================================
    // ANIMAL LOST
    // =========================================================

    public void UnregisterTrackedAnimal(IAnimalAction animal)
    {
        if (animal == null) return;

        // A late "lost" event from an already-hidden card is ignored here
        if (!activeTrackedAnimals.Contains(animal)) return;

        activeTrackedAnimals.Remove(animal);

        Debug.Log($"[ARAnimalManager] Unregistered: {animal.AnimalName}. " +
                  $"Total tracked: {activeTrackedAnimals.Count}");

        OnAnimalLost?.Invoke(animal);

        if (ReferenceEquals(selectedAnimal, animal))
        {
            IAnimalAction next = null;

            if (activeTrackedAnimals.Count > 0)
            {
                next = activeTrackedAnimals[activeTrackedAnimals.Count - 1];
            }

            // null if nothing is tracked, so buttons can hide
            SelectAnimal(next);
        }
    }

    // =========================================================
    // SELECTION
    // =========================================================

    public void SelectAnimal(IAnimalAction animal)
    {
        if (ReferenceEquals(selectedAnimal, animal)) return;

        selectedAnimal = animal;
        OnSelectedAnimalChanged?.Invoke(selectedAnimal);
    }

    public void ClearSelectedAnimal()
    {
        SelectAnimal(null);
    }

    // =========================================================
    // ACTIONS (buttons)
    // =========================================================

    public void TriggerAction()
    {
        if (!HasSelected("perform action")) return;
        selectedAnimal.PlayActionAnimation();
    }

    public void TriggerAudio()
    {
        if (!HasSelected("play audio")) return;
        selectedAnimal.PlayAudioWithAnimation();
    }

    public void TriggerMove()
    {
        if (!HasSelected("move")) return;
        selectedAnimal.PlayMove();
    }

    public void TriggerIdle()
    {
        if (!HasSelected("idle")) return;
        selectedAnimal.PlayIdle();
    }

    public void TriggerActionOnAll()
    {
        foreach (IAnimalAction animal in activeTrackedAnimals)
        {
            if (animal != null) animal.PlayActionAnimation();
        }
    }

    public void TriggerAudioOnAll()
    {
        foreach (IAnimalAction animal in activeTrackedAnimals)
        {
            if (animal != null) animal.PlayAudioWithAnimation();
        }
    }

    private bool HasSelected(string what)
    {
        if (selectedAnimal != null && selectedAnimal.IsTracked) return true;

        Debug.LogWarning($"[ARAnimalManager] No tracked animal selected to {what}.");
        return false;
    }
}