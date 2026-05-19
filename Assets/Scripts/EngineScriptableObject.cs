using UnityEngine;

[CreateAssetMenu(fileName = "EngineData", menuName = "Train System/Engine Data")]
public class EngineScriptableObject : ScriptableObject
{
    [Header("Engine Settings")]
    [SerializeField] private float engineLength = 2.52f;
    [SerializeField] private Vector3 enginePosition = new Vector3(2.52f, 0.42f, -0.31f);
    public float EngineLength => engineLength;
    public Vector3 EnginePosition => enginePosition;
    [SerializeField] public bool inOverdrive = false;
}