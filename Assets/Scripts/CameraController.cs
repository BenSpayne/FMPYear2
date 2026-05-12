using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TrainManager trainManager;
    [SerializeField] private Transform trainEngine;
    
    [Header("Camera Settings")]
    [SerializeField] private float smoothSpeed = 3f;
    [SerializeField] private float moveSmoothSpeed = 2f;
    
    [Header("Base Camera Position")]
    [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 3f, -8f);
    
    [Header("Camera Adjustment Per Carriage")]
    [SerializeField] private float extraHeightPerCarriage = 0.2f; // Move camera up per carriage
    [SerializeField] private float extraDistancePerCarriage = 0.4f; // Move camera back per carriage
    [SerializeField] private float maxExtraHeight = 3f; // Maximum upward movement
    [SerializeField] private float maxExtraDistance = 5f; // Maximum backward movement
    
    [Header("Field of View Adjustment")]
    [SerializeField] private float extraFOVPerCarriage = 2f; // Increase FOV per carriage
    [SerializeField] private float maxExtraFOV = 15f; // Maximum FOV increase
    [SerializeField] private float fovSmoothSpeed = 2f;
    
    [Header("When to Start Adjusting")]
    [SerializeField] private int startAdjustAtCarriageCount = 1; // Start adjusting after this many carriages (excluding engine)
    
    private Camera cam;
    private Vector3 targetOffset;
    private float targetFOV;
    private float defaultFOV;
    
    private void Start()
    {
        cam = GetComponent<Camera>();
        
        if (trainManager == null)
            trainManager = FindFirstObjectByType<TrainManager>();
        
        if (trainEngine == null)
        {
            GameObject engine = GameObject.FindGameObjectWithTag("TrainEngine");
            if (engine != null)
                trainEngine = engine.transform;
        }
        
        // Store default FOV
        if (cam != null)
            defaultFOV = cam.fieldOfView;
        
        // Initialize target values
        targetOffset = defaultOffset;
        targetFOV = defaultFOV;
    }
    
    public void UpdateCameraPosition(int carriageCount)
    {
        // Calculate how many carriages beyond the threshold
        int extraCarriages = Mathf.Max(0, carriageCount - startAdjustAtCarriageCount);
        
        // Calculate position offset
        float extraHeight = Mathf.Min(extraCarriages * extraHeightPerCarriage, maxExtraHeight);
        float extraDistance = Mathf.Min(extraCarriages * extraDistancePerCarriage, maxExtraDistance);
        
        // Build target offset
        targetOffset = new Vector3(
            defaultOffset.x,
            defaultOffset.y + extraHeight,
            defaultOffset.z - extraDistance  // Negative Z moves camera back
        );
        
        // Calculate FOV adjustment (perspective camera zooms by changing FOV)
        float extraFOV = Mathf.Min(extraCarriages * extraFOVPerCarriage, maxExtraFOV);
        targetFOV = defaultFOV + extraFOV;
        
        // Debug logging
        if (extraCarriages > 0)
        {
            Debug.Log($"Camera adjusting: {carriageCount} carriages | " +
                     $"Offset: Y={targetOffset.y:F1}, Z={targetOffset.z:F1} | " +
                     $"FOV: {targetFOV:F1}");
        }
    }
    
    private void LateUpdate()
    {
        if (trainEngine == null) return;
        
        // Calculate world target position (engine position + camera offset)
        Vector3 worldTargetPosition = trainEngine.position + targetOffset;
        
        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, worldTargetPosition, moveSmoothSpeed * Time.deltaTime);
        
        // Smoothly adjust Field of View for perspective camera
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.deltaTime);
        }
        
        // Look at the train engine
        transform.LookAt(trainEngine);
    }
    
    // Force immediate camera update (useful for instant changes)
    public void ForceCameraUpdate()
    {
        if (trainManager != null)
            UpdateCameraPosition(trainManager.GetCarriageCount());
        
        if (trainEngine != null)
        {
            transform.position = trainEngine.position + targetOffset;
            transform.LookAt(trainEngine);
            if (cam != null)
                cam.fieldOfView = targetFOV;
        }
    }
    
    // Visualize camera positions in editor
    private void OnDrawGizmosSelected()
    {
        if (trainEngine != null)
        {
            // Draw default position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(trainEngine.position + defaultOffset, 0.5f);
            
            // Draw max adjusted position
            Vector3 maxOffset = new Vector3(
                defaultOffset.x,
                defaultOffset.y + maxExtraHeight,
                defaultOffset.z - maxExtraDistance
            );
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(trainEngine.position + maxOffset, 0.5f);
            
            // Draw line showing camera movement range
            Gizmos.color = Color.white;
            Gizmos.DrawLine(trainEngine.position + defaultOffset, trainEngine.position + maxOffset);
            
            // Draw camera view cones (approximate)
            Gizmos.color = Color.cyan;
            float defaultFOVRad = defaultFOV * Mathf.Deg2Rad;
            float defaultViewHeight = Mathf.Tan(defaultFOVRad / 2) * 10f;
            Gizmos.DrawWireCube(trainEngine.position + defaultOffset + (Vector3.forward * 5f), new Vector3(defaultViewHeight * 1.33f, defaultViewHeight, 0.1f));
        }
    }
}