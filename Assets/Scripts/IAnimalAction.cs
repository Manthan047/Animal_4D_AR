using UnityEngine;

public interface IAnimalAction
{
    string AnimalName { get; }
    bool IsTracked { get; set; }
    void PlayActionAnimation();
    void PlayAudioWithAnimation();
    void PlayIdle();
    void PlayMove();
    AnimalData GetAnimalData();
}