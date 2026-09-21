using System;
using System.Collections.Generic;
using UnityEngine;

public class ARAnimalManager : MonoBehaviour
{

  


    public static ARAnimalManager Instance { get; private set; }

    public event Action<IAnimalAction> OnAnimalTracked;
    public event Action<IAnimalAction> OnAnimalLost;
    public event Action<IAnimalAction> OnSelectedAnimalChanged;

    private readonly List<IAnimalAction> activeTrackedAnimals =
        new List<IAnimalAction>();

    [Header("Selection Settings")]

    [Tooltip(
        "If true, a newly detected animal automatically becomes selected. " +
        "If false, the first detected animal is selected only when nothing is selected."
    )]
    [SerializeField] private bool autoSelectNewlyDetected = true;

    private IAnimalAction selectedAnimal;

    public IReadOnlyList<IAnimalAction> ActiveTrackedAnimals =>
        activeTrackedAnimals;

    public IAnimalAction SelectedAnimal =>
        selectedAnimal;

    public bool HasActiveAnimal =>
        activeTrackedAnimals.Count > 0;


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

        // IMPORTANT:
        // Start with nothing selected.
        selectedAnimal = null;
    }


    // =========================================================
    // ANIMAL TRACKED
    // =========================================================

    public void RegisterTrackedAnimal(IAnimalAction animal)
    {
        if (animal == null)
            return;

         Debug.Log($"[DEBUG] RegisterTrackedAnimal called for: {animal.AnimalName}"); 

        if (activeTrackedAnimals.Contains(animal))
            return;


        activeTrackedAnimals.Add(animal);

        Debug.Log(
            $"[ARAnimalManager] Animal registered: " +
            $"{animal.AnimalName}. " +
            $"Total tracked: {activeTrackedAnimals.Count}"
        );


        OnAnimalTracked?.Invoke(animal);


        // -----------------------------------------------------
        // SELECT DETECTED ANIMAL
        // -----------------------------------------------------

        if (selectedAnimal == null || autoSelectNewlyDetected)
        {
            SelectAnimal(animal);
        }
    }


    // =========================================================
    // ANIMAL LOST
    // =========================================================

    public void UnregisterTrackedAnimal(IAnimalAction animal)
    {
        if (animal == null)
            return;

        if (!activeTrackedAnimals.Contains(animal))
            return;


        activeTrackedAnimals.Remove(animal);

        Debug.Log(
            $"[ARAnimalManager] Animal unregistered: " +
            $"{animal.AnimalName}. " +
            $"Total tracked: {activeTrackedAnimals.Count}"
        );


        OnAnimalLost?.Invoke(animal);


        // -----------------------------------------------------
        // WAS THE SELECTED ANIMAL LOST?
        // -----------------------------------------------------

        if (ReferenceEquals(selectedAnimal, animal))
        {
            IAnimalAction nextAnimal = null;


            if (activeTrackedAnimals.Count > 0)
            {
                nextAnimal =
                    activeTrackedAnimals[
                        activeTrackedAnimals.Count - 1
                    ];
            }


            // If another animal is tracked, select it.
            // Otherwise selection becomes null.
            SelectAnimal(nextAnimal);
        }
    }


    // =========================================================
    // SELECT ANIMAL
    // =========================================================

    public void SelectAnimal(IAnimalAction animal)
    {
        // -----------------------------------------------------
        // If same animal is already selected, do nothing.
        // -----------------------------------------------------

        if (ReferenceEquals(selectedAnimal, animal))
            return;


        selectedAnimal = animal;


       


        OnSelectedAnimalChanged?.Invoke(selectedAnimal);
    }


    // =========================================================
    // CLEAR SELECTION
    // =========================================================

    public void ClearSelectedAnimal()
    {
        SelectAnimal(null);
    }


    // =========================================================
    // ACTION
    // =========================================================

    public void TriggerAction()
    {
        if (selectedAnimal == null)
        {
            Debug.LogWarning(
                "[ARAnimalManager] No animal selected to perform action."
            );

            return;
        }

        selectedAnimal.PlayActionAnimation();
    }


    // =========================================================
    // AUDIO
    // =========================================================

    public void TriggerAudio()
    {
        if (selectedAnimal == null)
        {
            Debug.LogWarning(
                "[ARAnimalManager] No animal selected to play audio."
            );

            return;
        }

        selectedAnimal.PlayAudioWithAnimation();
    }


    // =========================================================
    // MOVE
    // =========================================================

    public void TriggerMove()
    {
        if (selectedAnimal == null)
        {
            Debug.LogWarning(
                "[ARAnimalManager] No animal selected to move."
            );

            return;
        }

        selectedAnimal.PlayMove();
    }


    // =========================================================
    // IDLE
    // =========================================================

    public void TriggerIdle()
    {
        if (selectedAnimal == null)
        {
            Debug.LogWarning(
                "[ARAnimalManager] No animal selected for idle."
            );

            return;
        }

        selectedAnimal.PlayIdle();
    }


    // =========================================================
    // ACTION ON ALL
    // =========================================================

    public void TriggerActionOnAll()
    {
        foreach (IAnimalAction animal in activeTrackedAnimals)
        {
            if (animal != null)
            {
                animal.PlayActionAnimation();
            }
        }
    }


    // =========================================================
    // AUDIO ON ALL
    // =========================================================

    public void TriggerAudioOnAll()
    {
        foreach (IAnimalAction animal in activeTrackedAnimals)
        {
            if (animal != null)
            {
                animal.PlayAudioWithAnimation();
            }
        }
    }

}