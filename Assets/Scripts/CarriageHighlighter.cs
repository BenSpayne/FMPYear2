using UnityEngine;
using System.Collections.Generic;

public class CarriageHighlighter : MonoBehaviour
{
    [Header("HDRP Highlight Settings")]
    [SerializeField] private string outlineLayerName = "Outline";
    [SerializeField] private int outlineLayerIndex = 31; // Default custom layer
    
    private bool isHighlighted = false;
    private int originalLayer;
    
    void Start()
    {
        originalLayer = gameObject.layer;
    }
    
    void OnMouseEnter()
    {
        if (!isHighlighted)
        {
            Highlight();
        }
    }
    
    void OnMouseExit()
    {
        if (isHighlighted)
        {
            Unhighlight();
        }
    }
    
    void Highlight()
    {
        isHighlighted = true;
        // Add to outline layer so Custom Pass picks it up
        gameObject.layer = LayerMask.NameToLayer(outlineLayerName);
        if (gameObject.layer == -1) gameObject.layer = outlineLayerIndex;
    }
    
    void Unhighlight()
    {
        isHighlighted = false;
        // Remove from outline layer
        gameObject.layer = originalLayer;
    }
}