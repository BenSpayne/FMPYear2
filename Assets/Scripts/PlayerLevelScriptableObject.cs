using UnityEngine;

[CreateAssetMenu(fileName = "PlayerLevel", menuName = "Scriptable Objects/PlayerLevel")]
public class PlayerLevelScriptableObject : ScriptableObject
{
    [SerializeField] public int playerLevel = 0;
    [SerializeField] public double playerXP = 0;
}
