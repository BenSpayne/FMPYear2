using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainManager trainManager;
    [SerializeField] private CarriageScriptableObject carriagesData;
    
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI residentialCountText;
    [SerializeField] private TextMeshProUGUI medicalCountText;
    [SerializeField] private TextMeshProUGUI factoryCountText;
    [SerializeField] private TextMeshProUGUI wheatCountText;
    
    [Header("UI Display Settings")]
    [SerializeField] private string residentialPrefix = "Residential: ";
    [SerializeField] private string medicalPrefix = "Medical: ";
    [SerializeField] private string factoryPrefix = "Factory: ";
    [SerializeField] private string wheatPrefix = "Wheat: ";
    
    [SerializeField] private bool showTotalCount = true;
    [SerializeField] private TextMeshProUGUI totalCountText;
    
    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private int highlightThreshold = 0;
    
    [Header("Panel Fade Toggle")]
    [SerializeField] private CanvasGroup panelCanvasGroup; // The CanvasGroup to fade
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private bool startVisible = false;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private bool showDebugMessages = true;
    
    private Dictionary<string, TextMeshProUGUI> textMap;
    private bool isVisible;
    private Coroutine currentFadeCoroutine;
    
    void Start()
    {
        // Find TrainManager if not assigned
        if (trainManager == null)
            trainManager = FindFirstObjectByType<TrainManager>();
        
        if (trainManager == null)
        {
            Debug.LogError("TrainManager not found! Please assign it in the inspector.");
        }
        
        // Initialize text map for easy lookup
        textMap = new Dictionary<string, TextMeshProUGUI>();
        textMap["Residential"] = residentialCountText;
        textMap["Medical"] = medicalCountText;
        textMap["Factory"] = factoryCountText;
        textMap["Wheat"] = wheatCountText;
        
        // Initialize CanvasGroup
        if (panelCanvasGroup != null)
        {
            isVisible = startVisible;
            panelCanvasGroup.alpha = isVisible ? 1f : 0f;
            panelCanvasGroup.interactable = isVisible;
            panelCanvasGroup.blocksRaycasts = isVisible;
            
            if (showDebugMessages)
                Debug.Log($"CanvasGroup initialized: {(isVisible ? "Visible" : "Hidden")}");
        }
        else
        {
            Debug.LogWarning("Panel CanvasGroup is not assigned!");
        }
        
        // Update UI immediately
        UpdateAllCounts();
    }
    
    void Update()
    {
        // Update counts every frame
        UpdateAllCounts();
        
        // Check for toggle key press
        if (panelCanvasGroup != null && Input.GetKeyDown(toggleKey))
        {
            TogglePanelFade();
        }
    }
    
    void TogglePanelFade()
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);
        
        isVisible = !isVisible;
        currentFadeCoroutine = StartCoroutine(FadeCanvasGroup(panelCanvasGroup, isVisible ? 1f : 0f, fadeDuration));
        
        if (showDebugMessages)
            Debug.Log($"Panel {(isVisible ? "fading in" : "fading out")}");
    }
    
    IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            // Use SmoothStep for nicer easing
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, easedT);
            
            yield return null;
        }
        
        canvasGroup.alpha = targetAlpha;
        
        // Enable/disable interaction based on visibility
        canvasGroup.interactable = targetAlpha > 0.5f;
        canvasGroup.blocksRaycasts = targetAlpha > 0.5f;
        
        if (showDebugMessages)
            Debug.Log($"Fade complete. Alpha: {canvasGroup.alpha:F2}, Interactable: {canvasGroup.interactable}");
    }
    
    // Public method to fade in the panel
    public void FadeInPanel()
    {
        if (panelCanvasGroup == null) return;
        
        if (!isVisible)
        {
            if (currentFadeCoroutine != null)
                StopCoroutine(currentFadeCoroutine);
            
            isVisible = true;
            currentFadeCoroutine = StartCoroutine(FadeCanvasGroup(panelCanvasGroup, 1f, fadeDuration));
        }
    }
    
    // Public method to fade out the panel
    public void FadeOutPanel()
    {
        if (panelCanvasGroup == null) return;
        
        if (isVisible)
        {
            if (currentFadeCoroutine != null)
                StopCoroutine(currentFadeCoroutine);
            
            isVisible = false;
            currentFadeCoroutine = StartCoroutine(FadeCanvasGroup(panelCanvasGroup, 0f, fadeDuration));
        }
    }
    
    // Public method to toggle from external scripts
    public void TogglePanelFadeExternal()
    {
        TogglePanelFade();
    }
    
    // Public method to check if panel is visible
    public bool IsPanelVisible()
    {
        return isVisible;
    }
    
    // Public method to set panel visibility instantly (no fade)
    public void SetPanelVisibleInstant(bool visible)
    {
        if (panelCanvasGroup == null) return;
        
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);
        
        isVisible = visible;
        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }
    
    public void UpdateAllCounts()
    {
        if (carriagesData != null)
        {
            UpdateCountFromScriptableObject("Residential", residentialCountText, residentialPrefix);
            UpdateCountFromScriptableObject("Medical", medicalCountText, medicalPrefix);
            UpdateCountFromScriptableObject("Factory", factoryCountText, factoryPrefix);
            UpdateCountFromScriptableObject("Wheat", wheatCountText, wheatPrefix);
        }
        else if (trainManager != null)
        {
            UpdateCountFromTrainManager();
        }
        
        // Update total count
        if (showTotalCount && totalCountText != null && trainManager != null)
        {
            int total = trainManager.GetCarriageCount();            
            if (total > highlightThreshold)
                totalCountText.color = highlightColor;
            else
                totalCountText.color = normalColor;
        }
    }
    
    private void UpdateCountFromScriptableObject(string tag, TextMeshProUGUI textComponent, string prefix)
    {
        if (textComponent == null) return;
        
        int count = GetCountFromScriptableObject(tag);
        textComponent.text = $"{prefix}{count}";
        
        if (count > highlightThreshold)
            textComponent.color = highlightColor;
        else
            textComponent.color = normalColor;
    }
    
    private int GetCountFromScriptableObject(string tag)
    {
        if (carriagesData == null) return 0;
        
        var carriageCount = carriagesData.carriageCounts.Find(c => c.carriageTag == tag);
        return carriageCount != null ? carriageCount.count : 0;
    }
    
    private void UpdateCountFromTrainManager()
    {
        List<string> carriageOrder = trainManager.GetCarriageOrder();
        
        int residentialCount = 0;
        int medicalCount = 0;
        int factoryCount = 0;
        int wheatCount = 0;
        
        for (int i = 1; i < carriageOrder.Count; i++)
        {
            switch (carriageOrder[i].ToLower())
            {
                case "residential":
                    residentialCount++;
                    break;
                case "medical":
                    medicalCount++;
                    break;
                case "factory":
                    factoryCount++;
                    break;
                case "wheat":
                    wheatCount++;
                    break;
            }
        }
        
        if (residentialCountText != null)
            residentialCountText.text = $"{residentialPrefix}{residentialCount}";
        if (medicalCountText != null)
            medicalCountText.text = $"{medicalPrefix}{medicalCount}";
        if (factoryCountText != null)
            factoryCountText.text = $"{factoryPrefix}{factoryCount}";
        if (wheatCountText != null)
            wheatCountText.text = $"{wheatPrefix}{wheatCount}";
    }
    
    public void UpdateResidentialCount(int count)
    {
        if (residentialCountText != null)
        {
            residentialCountText.text = $"{residentialPrefix}{count}";
            residentialCountText.color = count > highlightThreshold ? highlightColor : normalColor;
        }
    }
    
    public void UpdateMedicalCount(int count)
    {
        if (medicalCountText != null)
        {
            medicalCountText.text = $"{medicalPrefix}{count}";
            medicalCountText.color = count > highlightThreshold ? highlightColor : normalColor;
        }
    }
    
    public void UpdateFactoryCount(int count)
    {
        if (factoryCountText != null)
        {
            factoryCountText.text = $"{factoryPrefix}{count}";
            factoryCountText.color = count > highlightThreshold ? highlightColor : normalColor;
        }
    }
    
    public void UpdateWheatCount(int count)
    {
        if (wheatCountText != null)
        {
            wheatCountText.text = $"{wheatPrefix}{count}";
            wheatCountText.color = count > highlightThreshold ? highlightColor : normalColor;
        }
    }
    
    public void RefreshCounts()
    {
        UpdateAllCounts();
    }
}