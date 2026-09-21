using UnityEngine;

public class ActionBroadcastManager : MonoBehaviour
{
    public void PlayActionOnActiveAnimals()
    {
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.TriggerAction();
            return;
        }

        // Fallback polymorphism
        var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb is IAnimalAction animal && mb.gameObject.activeInHierarchy)
            {
                animal.PlayActionAnimation();
            }
        }
    }

    public void PlayAudioOnActiveAnimals()
    {
        if (ARAnimalManager.Instance != null)
        {
            ARAnimalManager.Instance.TriggerAudio();
            return;
        }

        var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var mb in allBehaviours)
        {
            if (mb is IAnimalAction animal && mb.gameObject.activeInHierarchy)
            {
                animal.PlayAudioWithAnimation();
            }
        }
    }
}