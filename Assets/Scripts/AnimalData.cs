using UnityEngine;

[CreateAssetMenu(fileName = "NewAnimalData", menuName = "AR Animals/Animal Data", order = 1)]
public class AnimalData : ScriptableObject
{
    [Header("General Info")]
    public string animalName;
    public string scientificName;
    public string classification;
    public string era;

    [Header("Physical Attributes")]
    public string averageLength;
    public string averageWeight;

    [Header("Educational Information")]
    [TextArea(4, 12)]
    public string description;

    [TextArea(2, 6)]
    public string funFact;

    [Header("Visuals")]
    [Tooltip("The small icon sprite shown in the bottom animal-picker row.")]
    public Sprite thumbnail;

    [Tooltip("The full 2D illustration shown when the user toggles to 2D view.")]
    public Sprite image2D;

    [Header("Audio")]
    public AudioClip roarClip;
    public AudioClip ambientClip;

    [Tooltip("Narration/voiceover audio that reads out the description text aloud.")]
    public AudioClip descriptionAudioClip;

    [Header("AR Configuration")]
    public Vector3 targetScale = Vector3.one;
    public Vector3 targetRotationOffset = Vector3.zero;

    [Header("Preview Stage")]
    [Tooltip("Standalone model prefab shown in the rotatable mini 3D viewer, independent of AR marker tracking.")]
    public GameObject previewModelPrefab;
}