using UnityEngine;

[CreateAssetMenu(fileName = "GameProgress", menuName = "Scriptable Objects/GameProgress")]
public class GameProgressScriptableObject : ScriptableObject
{
    [Header("Resources")]
    [SerializeField] public int materialCounter = 25;
    [SerializeField] public int coinCounter = 30;
    [SerializeField] public int population = 0;
    [SerializeField] public int hungerPercent = 100;
    [SerializeField] public int happinessPercent = 100;
    [SerializeField] public int healthPrecent = 100;
    [SerializeField] public int productivityPrecent = 100;

    [Header("Carriage Base Levels")]
    [SerializeField] public int residentialBaseLevel = 1;
    [SerializeField] public int factoryBaseLevel = 1;
    [SerializeField] public int wheatBaseLevel = 1;

    [Header("Engine Stats")]
    [SerializeField] public int engineLevel = 1;
    [SerializeField] public int engineHealth = 3;
    [SerializeField] public int trainSpeed = 3;
}
