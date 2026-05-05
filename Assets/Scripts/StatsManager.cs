using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class StatsManager : MonoBehaviour
{
    // Player Stats
    public int playerLevel = 0;
    public TextMeshProUGUI playerLevelUI;
    public double XP = 0;
    public int materials = 10;
    public int coins = 10;
    public int wheat = 5;

    // Population Stats

    public int population = 250;
    public TextMeshProUGUI populationUI;
    public int hungerPercent = 100;
    public int happinessPercent = 100;
    public int healthPrecent = 100;
    public int productivityPrecent = 100;

    // Train Stats

    public bool inOverdrive = false;
    public TextMeshProUGUI overdriveText;
    public int trainSpeed = 3;
    public int engineHealth = 3;
    [SerializeField] private Image engineSpeedBar1;
    [SerializeField] private Image engineSpeedBar2;
    [SerializeField] private Image engineSpeedBar3;
    public TextMeshProUGUI healthGood;
    public TextMeshProUGUI healthOkay;
    public TextMeshProUGUI healthCritical;
    [SerializeField] private Color greenColor = new Color(67, 219, 0);
    [SerializeField] private Color orangeColor = new Color(219, 113, 0);
    [SerializeField] private Color redColor = new Color(219, 2, 0);



    void SetCountText()
    {
        playerLevelUI.text = playerLevel.ToString();
        populationUI.text = population.ToString();
    }

    void overdriveUpdate()
    {
        if (inOverdrive == true)
        {
            overdriveText.enabled = true;
        }
        else
        {
            overdriveText.enabled = false;
        }
    }

    void updateEngineSpeedBar()
    {
        switch (trainSpeed)
        {
            case 3:
                engineSpeedBar1.enabled = true;
                engineSpeedBar2.enabled = true;
                engineSpeedBar3.enabled = true;

                engineSpeedBar1.color = greenColor;
                engineSpeedBar2.color = greenColor;
                engineSpeedBar3.color = greenColor;
                break;

            case 2:
                engineSpeedBar1.enabled = true;
                engineSpeedBar2.enabled = true;
                engineSpeedBar3.enabled = false;

                engineSpeedBar1.color = orangeColor;
                engineSpeedBar2.color = orangeColor;
                break;

            case 1:
                engineSpeedBar1.enabled = true;
                engineSpeedBar2.enabled = false;
                engineSpeedBar3.enabled = false;

                engineSpeedBar1.color = redColor;
                break;
        }
    }

    void updateEngineHealth()
    {
        switch (engineHealth)
        {
            case 3:
                healthGood.enabled = true;
                healthOkay.enabled = false;
                healthCritical.enabled = false;

                healthGood.color = greenColor;
                break;

            case 2:
                healthGood.enabled = false;
                healthOkay.enabled = true;
                healthCritical.enabled = false;

                healthOkay.color = orangeColor;
                break;

            case 1:
                healthGood.enabled = false;
                healthOkay.enabled = false;
                healthCritical.enabled = true;

                healthCritical.color = redColor;
                break;
        }
    }



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetCountText();
        updateEngineSpeedBar();
        updateEngineHealth();
        overdriveUpdate();
    }

    // Update is called once per frame
    void Update()
    {
        SetCountText();
        updateEngineSpeedBar();
        updateEngineHealth();
        overdriveUpdate();
    }
}
