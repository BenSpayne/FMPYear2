using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Carriages", menuName = "Train System/Carriages")]
public class CarriageScriptableObject : ScriptableObject
{
    public List<CarriageCount> carriageCounts = new List<CarriageCount>();
    
    [System.Serializable]
    public class CarriageCount
    {
        public string carriageTag; // "Residential", "Medical", "Factory", "Wheat"
        public int count;
    }
    
    // Helper method to get count by tag
    public int GetCount(string tag)
    {
        CarriageCount found = carriageCounts.Find(c => c.carriageTag == tag);
        return found != null ? found.count : 0;
    }
    
    // Helper method to set count by tag
    public void SetCount(string tag, int count)
    {
        CarriageCount found = carriageCounts.Find(c => c.carriageTag == tag);
        if (found != null)
            found.count = count;
        else
            carriageCounts.Add(new CarriageCount { carriageTag = tag, count = count });
    }
}