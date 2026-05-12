using UnityEngine;

public class CarriageController : MonoBehaviour
{
    private int carriageIndex;
    
    public void SetCarriageIndex(int index)
    {
        carriageIndex = index;
        // Update any visual elements that depend on position
        gameObject.name = $"Carriage_{index}_{GetType().Name}";
    }
    
    public int GetCarriageIndex()
    {
        return carriageIndex;
    }
}