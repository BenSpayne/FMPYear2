using UnityEngine;
using Unity.Cinemachine;

public class CameraEffect : MonoBehaviour
{
    [Header("Settings")]
    public float sensitivity = 1f;
    public float smoothSpeed = 8f;
    public Vector2 rotationLimit = new Vector2(5f, 3f); // Limit in world units
    
    [Header("Cinemachine Reference")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float returnSpeed = 3f;
    private Vector2 targetOffset;
    private Vector2 currentOffset;
    private Vector2 lastMousePosition;
    private bool isRotating = false;
   [SerializeField]  private CinemachineHardLookAt cinemachineHardLookAt;
    
    void Start()
    {
        // Get the CinemachineCamera component
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();
        
        if (cinemachineCamera == null)
        {
            Debug.LogError("CinemachineCamera component not found!");
            return;
        }
        
        // Get the Hard Look At component
        cinemachineHardLookAt = cinemachineCamera.GetComponent<CinemachineHardLookAt>();
        
        if (cinemachineHardLookAt == null)
        {
            Debug.LogError("Cinemachine Hard Look At component not found on the camera!");
            return;
        }
        
        // Set the Look At target
        GameObject engine = GameObject.FindGameObjectWithTag("TrainEngine");
        if (engine != null)
        {
            cinemachineCamera.LookAt = engine.transform;
            Debug.Log("Camera now looking at: TrainEngine");
        }
        
        // Initialize offsets
        targetOffset = Vector2.zero;
        currentOffset = Vector2.zero;
        
        Debug.Log("CameraEffect initialized. Hold Right Mouse Button and move mouse to adjust Look At offset.");
    }
    
    void Update()
    {
        if (cinemachineCamera == null || cinemachineHardLookAt == null) return;
        
        // Hold right mouse button to adjust offset
        if (Input.GetMouseButtonDown(1))
        {
            isRotating = true;
            lastMousePosition = Input.mousePosition;
            Debug.Log("Camera offset adjustment activated");
        }
        
        if (Input.GetMouseButtonUp(1))
        {
            isRotating = false;
            Debug.Log("Camera offset adjustment deactivated");
        }
        
        if (isRotating)
        {
            // Get mouse movement
            Vector2 mouseDelta = (Vector2)Input.mousePosition - lastMousePosition;
            
            // Calculate target offset (invert Y for natural feel)
            targetOffset.x += mouseDelta.x * sensitivity * Time.deltaTime;
            targetOffset.y -= mouseDelta.y * sensitivity * Time.deltaTime;
            
            // Clamp offset limits
            targetOffset.x = Mathf.Clamp(targetOffset.x, -rotationLimit.x, rotationLimit.x);
            targetOffset.y = Mathf.Clamp(targetOffset.y, -rotationLimit.y, rotationLimit.y);
            
            // Update last mouse position
            lastMousePosition = Input.mousePosition;
        }
        else
        {
            // Smoothly return to zero when not rotating
            targetOffset = Vector2.Lerp(targetOffset, Vector2.zero, returnSpeed * Time.deltaTime);
        }

        // Smooth the offset
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, smoothSpeed * Time.deltaTime);

        // Apply the offset to Cinemachine Hard Look At component using LookAtOffset
        ApplyOffsetToHardLookAt();
    }

    void ApplyOffsetToHardLookAt()
    {
        if (cinemachineHardLookAt == null) return;

        // Use LookAtOffset property (as shown in your reference)
        cinemachineHardLookAt.LookAtOffset = new Vector3(currentOffset.x, currentOffset.y, 0);

        // For debugging
        if (currentOffset.magnitude > 0.01f)
        {
           
           Debug.LogWarning($"Hard Look At LookAtOffset - X: {currentOffset.x:F2}, Y: {currentOffset.y:F2}");
        }
        else
        {
           Debug.LogError($" " + currentOffset.magnitude  +" X: {currentOffset.x:F2}, Y: {currentOffset.y:F2}");
        }
    }
    
    // Reset camera offset to zero
    public void ResetCameraView()
    {
        targetOffset = Vector2.zero;
        currentOffset = Vector2.zero;
        
        if (cinemachineHardLookAt != null)
        {
            cinemachineHardLookAt.LookAtOffset = Vector3.zero;
        }
        
        Debug.Log("Camera offset reset to center");
    }
    
    // Visual debug in editor
    private void OnDrawGizmosSelected()
    {
        if (cinemachineCamera != null && cinemachineCamera.LookAt != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, cinemachineCamera.LookAt.position);
            Gizmos.DrawWireSphere(cinemachineCamera.LookAt.position, 0.5f);
        }
    }
}