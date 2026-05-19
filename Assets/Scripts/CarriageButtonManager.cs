using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI; // Add this for Button component

public class CarriageButtonManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainManager trainManager;
    
    [Header("Button Configuration")]
    [SerializeField] private List<CarriageButtonConfig> buttons;
    
    [Header("Feedback Settings")]
    [SerializeField] private Color clickColor = Color.green;
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private bool showDebugMessages = true;
    
    private Dictionary<Button, CarriageButtonConfig> buttonMap = new Dictionary<Button, CarriageButtonConfig>();
    private Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    
    [System.Serializable]
    public class CarriageButtonConfig
    {
        [Header("Button Info")]
        public string carriageTag; // "Residential", "Medical", "Factory", "Wheat"
        public Button button; // Changed from TMP_Button to Button
        public TextMeshProUGUI buttonText;
        
        [Header("Display")]
        public string buttonPrefix = "Add ";
        public string buttonSuffix = "";
    }
    
    void Start()
    {
        // Find TrainManager if not assigned
        if (trainManager == null)
        {
            trainManager = FindFirstObjectByType<TrainManager>();
            if (trainManager == null)
            {
                Debug.LogError("TrainManager not found! Please assign it in the inspector.");
                return;
            }
        }
        
        // Initialize all buttons
        InitializeButtons();
    }
    
    void InitializeButtons()
    {
        int initializedCount = 0;
        
        foreach (var config in buttons)
        {
            if (config.button == null)
            {
                Debug.LogWarning($"Button not assigned for {config.carriageTag}");
                continue;
            }
            
            // Find text component if not assigned
            if (config.buttonText == null)
            {
                config.buttonText = config.button.GetComponentInChildren<TextMeshProUGUI>();
                if (config.buttonText == null)
                {
                    Debug.LogWarning($"No TextMeshProUGUI found on button for {config.carriageTag}");
                }
            }
            
            // Set button text
            if (config.buttonText != null && string.IsNullOrEmpty(config.buttonText.text))
            {
                config.buttonText.text = $"{config.buttonPrefix}{config.carriageTag}{config.buttonSuffix}";
            }
            
            // Store original color
            if (config.buttonText != null)
            {
                originalColors[config.button] = config.buttonText.color;
            }
            
            // Add click listener
            string tag = config.carriageTag; // Capture for lambda
            config.button.onClick.AddListener(() => OnButtonClick(tag, config.button));
            
            // Store in dictionary for quick lookup
            buttonMap[config.button] = config;
            initializedCount++;
            
            if (showDebugMessages)
                Debug.Log($"Initialized button for {config.carriageTag} carriage");
        }
        
        Debug.Log($"Initialized {initializedCount} carriage buttons");
    }
    
    void OnButtonClick(string carriageTag, Button button)
    {
        if (trainManager == null)
        {
            Debug.LogError("TrainManager is null!");
            return;
        }
        
        if (showDebugMessages)
            Debug.Log($"Add {carriageTag} carriage button clicked!");
        
        // Flash button for visual feedback
        FlashButton(button);
        
        // Add the carriage
        trainManager.AddCarriage(carriageTag);
    }
    
    void FlashButton(Button button)
    {
        if (buttonMap.TryGetValue(button, out CarriageButtonConfig config) && config.buttonText != null)
        {
            // Store current color if not already stored
            if (!originalColors.ContainsKey(button))
            {
                originalColors[button] = config.buttonText.color;
            }
            
            // Change color
            config.buttonText.color = clickColor;
            
            // Start coroutine to reset color
            StartCoroutine(ResetButtonColor(config.buttonText, originalColors[button]));
        }
    }
    
    IEnumerator ResetButtonColor(TextMeshProUGUI text, Color originalColor)
    {
        yield return new WaitForSeconds(flashDuration);
        if (text != null)
            text.color = originalColor;
    }
    
    // Public method to update button text dynamically
    public void UpdateButtonText(string carriageTag, string newText)
    {
        foreach (var config in buttons)
        {
            if (config.carriageTag == carriageTag && config.buttonText != null)
            {
                config.buttonText.text = newText;
                break;
            }
        }
    }
    
    // Public method to enable/disable specific buttons
    public void SetButtonEnabled(string carriageTag, bool enabled)
    {
        foreach (var config in buttons)
        {
            if (config.carriageTag == carriageTag && config.button != null)
            {
                config.button.interactable = enabled;
                break;
            }
        }
    }
    
    // Public method to enable/disable all buttons
    public void SetAllButtonsEnabled(bool enabled)
    {
        foreach (var config in buttons)
        {
            if (config.button != null)
            {
                config.button.interactable = enabled;
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        foreach (var config in buttons)
        {
            if (config.button != null)
            {
                config.button.onClick.RemoveAllListeners();
            }
        }
    }
}