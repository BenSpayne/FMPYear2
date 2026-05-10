using UnityEngine;
using System;

[CreateAssetMenu(fileName = "Engine", menuName = "Scriptable Objects/Engine")]
public class EngineScriptableObject : ScriptableObject
{
    // Carriages
    private int factoryCarriages = 0;
    private int wheatGeneratorCarriages = 0;
    private int entertainmentCarriages = 0;
    private int medicalwardCarriages = 0;
    private int residentialCarriages = 0;

    // Engine
    [SerializeField] public int engineSpeed = 3;
    public int engineHealth = 3;
    public int engineLevel = 1;
    public bool overdriveEnabled = false;
    public int overdriveCooldownTime = 60;
    private int engineCarriage = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
