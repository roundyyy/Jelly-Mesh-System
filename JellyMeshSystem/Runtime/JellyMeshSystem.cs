using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Renderer))]  // More flexible requirement - either MeshRenderer or SkinnedMeshRenderer
public class JellyMeshSystem : MonoBehaviour, IDisposable
{
    [Header("Jelly Physics Settings")]
    [Range(0.1f, 20f)]
    public float intensity = 20f;         // Overall strength of jelly effect
    [Range(0.1f, 20f)]
    public float mass = 1.0f;            // Higher = more inertia, slower response
    [Range(0.1f, 50.0f)]
    public float stiffness = 30f;        // Return to original shape strength
    [Range(0.1f, 30.0f)]
    public float damping = 0.6f;           // How quickly oscillations settle

    [Header("Distance Settings")]
    public bool maintainRadius = true;     // Keep vertices at consistent distance from pivot
    [Range(0.01f, 10f)]
    public float distanceFalloff = 1.75f;   // General falloff for distance effects
    [Range(0.0f, 1.0f)]
    public float radiusConstraintStrength = 0.6f; // Strength of radius constraint (0=none, 1=strict)

    [Header("Response Settings")]
    [Range(0.1f, 10f)]
    public float movementInfluence = 10.0f; // Movement response strength
    [Range(0.01f, 10f)]
    public float falloff = 1.5f;           // Distance falloff for effects

    [Header("Collider Settings")]
    public bool updateMeshCollider = false;  // Whether to update the mesh collider with deformation
    [Range(1, 20)]
    public int colliderUpdateInterval = 20; // Update collider every N frames for performance

    [Header("Performance Settings")]
    [Range(0.0f, 1.0f)]
    public float performanceLevel = 0.0f;    // 0 = highest quality, 1 = highest performance

    [Header("LOD Settings")]
    public bool useLOD = false;             // Enable automatic LOD based on camera distance
    [Range(10f, 200f)]
    public float maxLODDistance = 100f;     // Distance at which effect is completely disabled
    [Range(1f, 50f)]
    public float minLODDistance = 5f;       // Distance below which full quality is used
    [Tooltip("Show debug visualization for LOD ranges and pivot")]
    public bool showGizmos = true;          // Toggle for gizmo visualization

    [Header("Custom Pivot")]
    [Tooltip("Optional custom pivot point (must be a child of this object)")]
    public Transform customPivot; // Optional custom pivot point

    [Header("Skinned Mesh Settings")]
    [Tooltip("How often to update the base skinned mesh (frames)")]
    [Range(1, 10)]
    public int skinnedMeshUpdateInterval = 1; // How often to update the base skinned mesh

    // Internal variables
    private Mesh originalMesh;
    private Mesh deformableMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Camera mainCamera;

    // Skinned mesh specific variables
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private bool useSkinnedMesh = false;
    private Mesh tempBakedMesh;
    private int skinnedUpdateCounter = 0;

    // Native arrays for the job system
    private NativeArray<float3> originalVertices;
    private NativeArray<float3> deformedVertices;
    private NativeArray<float3> vertexVelocities;
    private NativeArray<float> originalDistances;
    private NativeArray<float3> previousWorldVertices;
    private NativeArray<float3> currentWorldVertices;
    private float3 localCenterOfMass;

    // Motion tracking
    private float3 prevPosition;
    private quaternion prevRotation;

    // Job handle
    private JobHandle vertexUpdateJobHandle;

    // Flags
    private bool initialized = false;
    private bool jobScheduled = false;
    private int frameCounter = 0;  // Used for collider update interval

    // Performance management
    private int updateInterval = 0;
    private float timeScale = 1.0f;
    private int updateFrameCounter = 0;
    private float accumulatedTime = 0f;
    private float maxTimeStep = 1.0f / 30.0f; // Maximum time step for stability
    private int batchSize = 64;

    // LOD management
    private bool isEffectCulled = false;    // Whether the effect is currently culled due to LOD

    // Custom pivot tracking
    private float3 previousPivotPosition;
    private bool pivotInitialized = false;

    void Start()
    {
        // Find main camera for LOD
        if (useLOD && Camera.main != null)
        {
            mainCamera = Camera.main;
        }
        else if (useLOD)
        {
            Debug.LogWarning("LOD is enabled but Camera.main not found. Using first camera in scene.");
            mainCamera = FindObjectOfType<Camera>();

            if (mainCamera == null)
            {
                Debug.LogWarning("No cameras found in scene. Disabling LOD.");
                useLOD = false;
            }
        }

        // Detect which type of mesh we're using
        meshFilter = GetComponent<MeshFilter>();
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null)
        {
            useSkinnedMesh = true;
            Debug.Log("JellyMeshSystem: Using SkinnedMeshRenderer");

            // Create a temporary mesh for baking
            tempBakedMesh = new Mesh();

            // For skinned mesh, we need a MeshFilter and MeshRenderer to display our jelly result
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                // Copy materials from skinned mesh renderer
                meshRenderer.sharedMaterials = skinnedMeshRenderer.sharedMaterials;
            }

            // Initially disable the mesh renderer until we're ready
            meshRenderer.enabled = false;
        }
        else if (meshFilter != null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogError("MeshRenderer component is required when using MeshFilter!");
                enabled = false;
                return;
            }
            Debug.Log("JellyMeshSystem: Using MeshFilter/MeshRenderer");
        }
        else
        {
            Debug.LogError("Either MeshFilter or SkinnedMeshRenderer is required!");
            enabled = false;
            return;
        }

        // Get mesh collider if available and enabled
        if (updateMeshCollider)
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                Debug.LogWarning("MeshCollider updating is enabled but no MeshCollider component found. Adding one.");
                meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.convex = true;  // Ensure it's convex since that's what user expects
            }
            else if (!meshCollider.convex)
            {
                Debug.LogWarning("MeshCollider must be convex for runtime updates. Setting to convex.");
                meshCollider.convex = true;
            }
        }

        // Store original mesh
        if (useSkinnedMesh)
        {
            // For skinned mesh, bake the current state to start with
            originalMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(originalMesh);
        }
        else
        {
            // Regular mesh - get from mesh filter
            originalMesh = meshFilter.sharedMesh;
        }

        // Check if mesh is readable
        if (originalMesh == null)
        {
            Debug.LogError("Mesh is null! Make sure the object has a valid mesh.");
            enabled = false;
            return;
        }

        try
        {
            // This will throw an exception if the mesh is not readable
            Vector3[] testVertices = originalMesh.vertices;

            // Create a copy of the mesh that we can deform
            deformableMesh = Instantiate(originalMesh);
            meshFilter.mesh = deformableMesh;

            // Also assign the deformable mesh to the collider if we're updating it
            if (updateMeshCollider && meshCollider != null)
            {
                meshCollider.sharedMesh = deformableMesh;
            }

            InitializeNativeArrays();

            // Initialize motion tracking
            prevPosition = transform.position;
            prevRotation = transform.rotation;

            initialized = true;

            // Initialize the pivot tracking if a custom pivot is specified
            if (customPivot != null)
            {
                previousPivotPosition = transform.InverseTransformPoint(customPivot.position);
                pivotInitialized = true;
            }

            // Initialize world vertices
            UpdateWorldVertices();

            // For skinned mesh, enable our custom renderer and disable the original
            if (useSkinnedMesh)
            {
                skinnedMeshRenderer.enabled = false;
                meshRenderer.enabled = true;
            }

            // Debug info
            Debug.Log("JellyMeshSystem initialized with Job System. Vertex count: " + originalVertices.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Mesh is not readable! Set 'Read/Write Enabled' in the model's import settings. Error: " + e.Message);
            enabled = false;
        }
    }

    void InitializeNativeArrays()
    {
        // Get vertices from the original mesh
        Vector3[] meshVertices = originalMesh.vertices;

        // Initialize native arrays with the Persistent allocator since they need to live across frames
        originalVertices = new NativeArray<float3>(meshVertices.Length, Allocator.Persistent);
        deformedVertices = new NativeArray<float3>(meshVertices.Length, Allocator.Persistent);
        vertexVelocities = new NativeArray<float3>(meshVertices.Length, Allocator.Persistent);
        originalDistances = new NativeArray<float>(meshVertices.Length, Allocator.Persistent);
        previousWorldVertices = new NativeArray<float3>(meshVertices.Length, Allocator.Persistent);
        currentWorldVertices = new NativeArray<float3>(meshVertices.Length, Allocator.Persistent);

        // Convert Vector3[] to NativeArray<float3>
        for (int i = 0; i < meshVertices.Length; i++)
        {
            originalVertices[i] = meshVertices[i];
            deformedVertices[i] = meshVertices[i];
            vertexVelocities[i] = float3.zero;
        }

        // Calculate center of mass
        CalculateCenterOfMass();

        // Store original distances
        StoreOriginalDistances();
    }

    void CalculateCenterOfMass()
    {
        if (customPivot != null)
        {
            // Use custom pivot position in local space
            localCenterOfMass = transform.InverseTransformPoint(customPivot.position);
        }
        else
        {
            // Calculate average position of all vertices
            float3 sum = float3.zero;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                sum += originalVertices[i];
            }
            localCenterOfMass = sum / originalVertices.Length;
        }
    }

    void StoreOriginalDistances()
    {
        // Store the original distance of each vertex from the center of mass
        for (int i = 0; i < originalVertices.Length; i++)
        {
            originalDistances[i] = math.distance(originalVertices[i], localCenterOfMass);
        }
    }

    // Calculate performance settings based on performanceLevel or camera distance
    void UpdatePerformanceSettings()
    {
        float effectivePerformanceLevel = performanceLevel;
        bool shouldCull = false;

        // If LOD is enabled, calculate performance level based on camera distance
        if (useLOD && mainCamera != null)
        {
            float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);

            // Check if we should completely disable the effect (beyond max LOD distance)
            if (distanceToCamera >= maxLODDistance)
            {
                shouldCull = true;
                effectivePerformanceLevel = 1f; // Min quality if we need to transition back
            }
            // Map distance to performance level (closer = higher quality)
            else if (distanceToCamera <= minLODDistance)
            {
                shouldCull = false;
                effectivePerformanceLevel = 0f; // Max quality
            }
            else
            {
                shouldCull = false;
                // Linear interpolation between min and max distance
                effectivePerformanceLevel = Mathf.InverseLerp(minLODDistance, maxLODDistance, distanceToCamera);
            }
        }

        // Handle culling state changes
        if (shouldCull && !isEffectCulled)
        {
            // Effect is now culled, reset mesh to original state
            isEffectCulled = true;

            if (useSkinnedMesh)
            {
                // For skinned mesh, switch back to the original renderer
                if (skinnedMeshRenderer != null && meshRenderer != null)
                {
                    skinnedMeshRenderer.enabled = true;
                    meshRenderer.enabled = false;
                }
            }
            else
            {
                ResetMesh();
            }
        }
        else if (!shouldCull && isEffectCulled)
        {
            // Effect was culled but now should be enabled again
            isEffectCulled = false;

            if (useSkinnedMesh)
            {
                // Switch back to our jelly rendering
                if (skinnedMeshRenderer != null && meshRenderer != null)
                {
                    skinnedMeshRenderer.enabled = false;
                    meshRenderer.enabled = true;
                }
            }
        }

        // If culled, no need to do further calculations
        if (isEffectCulled)
            return;

        // Map performance level to update interval (0-5)
        // At performance 0, interval is 0 (update every frame)
        // At performance 1, interval is 5 (update every 6th frame)
        updateInterval = Mathf.RoundToInt(effectivePerformanceLevel * 5f);

        // Map performance level to time scale (1.0-0.7)
        // At performance 0, time scale is 1.0 (full speed)
        // At performance 1, time scale is 0.7 (reduced speed)
        timeScale = Mathf.Lerp(1.0f, 0.7f, effectivePerformanceLevel);

        // Calculate batch size based on performance level
        batchSize = Mathf.Max(32, Mathf.Min(512, originalVertices.Length / (SystemInfo.processorCount)));
    }

    void CheckPivotChange()
    {
        if (customPivot != null)
        {
            float3 currentPivotPosition = transform.InverseTransformPoint(customPivot.position);

            // Initialize the previous position if this is the first check
            if (!pivotInitialized)
            {
                previousPivotPosition = currentPivotPosition;
                pivotInitialized = true;
                return;
            }

            // Check if the pivot position has changed significantly
            // Using a small threshold to avoid floating point precision issues
            if (math.lengthsq(currentPivotPosition - previousPivotPosition) > 0.0001f)
            {
                // Update center of mass
                localCenterOfMass = currentPivotPosition;
                previousPivotPosition = currentPivotPosition;

                // Update original distances since they depend on center of mass
                StoreOriginalDistances();
            }
        }
    }

    void Update()
    {
        if (!initialized) return;

        // Check for pivot changes
        CheckPivotChange();

        // Update performance settings based on slider or LOD
        UpdatePerformanceSettings();

        // If effect is culled by LOD, skip all processing
        if (isEffectCulled)
            return;

        // Always accumulate time - we'll use this for physics no matter what
        accumulatedTime += Time.deltaTime * timeScale;

        // Make sure previous job is complete before accessing the data
        if (jobScheduled)
        {
            vertexUpdateJobHandle.Complete();
            jobScheduled = false;

            // Apply the deformation from the previous job
            ApplyDeformation();
        }

        // For skinned mesh, update from the animated mesh periodically
        if (useSkinnedMesh && skinnedMeshRenderer != null)
        {
            skinnedUpdateCounter = (skinnedUpdateCounter + 1) % skinnedMeshUpdateInterval;

            if (skinnedUpdateCounter == 0)
            {
                UpdateFromSkinnedMesh();
            }
        }

        // Skip processing if the mesh is not visible to save CPU
        Renderer visibilityRenderer = useSkinnedMesh ? meshRenderer : (Renderer)GetComponent<Renderer>();
        if (visibilityRenderer != null && !visibilityRenderer.isVisible)
        {
            // Still track motion for when it becomes visible again
            previousWorldVertices.CopyFrom(currentWorldVertices);
            UpdateWorldVertices();
            prevPosition = transform.position;
            prevRotation = transform.rotation;
            return;
        }

        // Update physics based on update interval
        // We use updateInterval+1 because if interval=0, we want to update every frame
        updateFrameCounter = (updateFrameCounter + 1) % (updateInterval + 1);

        // Only update physics when the counter reaches 0
        if (updateFrameCounter == 0)
        {
            // Always update world positions to track movement even if we don't process physics
            previousWorldVertices.CopyFrom(currentWorldVertices);
            UpdateWorldVertices();

            // Process physics with time scaling
            float effectiveTimeStep = accumulatedTime;

            // Enforce maximum time step for stability
            if (effectiveTimeStep > maxTimeStep)
            {
                effectiveTimeStep = maxTimeStep;
            }

            // Update physics with the effective time step
            UpdateVertexPhysics(effectiveTimeStep);

            // Reset accumulated time after physics update
            accumulatedTime = 0f;
        }
    }

    // Update the original vertices from the current state of the skinned mesh
    void UpdateFromSkinnedMesh()
    {
        if (tempBakedMesh == null)
        {
            tempBakedMesh = new Mesh();
        }

        // Bake the current skinned mesh state
        skinnedMeshRenderer.BakeMesh(tempBakedMesh);

        // Get the baked vertices
        Vector3[] bakedVertices = tempBakedMesh.vertices;

        // Only update if the vertex counts match
        if (bakedVertices.Length == originalVertices.Length)
        {
            // Update our original vertices to match the skinned state
            // We'll preserve any jelly deformation on top of this
            for (int i = 0; i < bakedVertices.Length; i++)
            {
                // Calculate how much the vertex is currently deformed by jelly physics
                float3 jellyOffset = deformedVertices[i] - originalVertices[i];

                // Update the original vertex to the new skinned position
                originalVertices[i] = bakedVertices[i];

                // Apply the same jelly offset to maintain consistency
                deformedVertices[i] = originalVertices[i] + jellyOffset;
            }

            // Update center of mass and distances if not using a custom pivot
            if (customPivot == null)
            {
                CalculateCenterOfMass();
                StoreOriginalDistances();
            }
        }
        else
        {
            Debug.LogWarning("Skinned mesh vertex count mismatch! Expected: " +
                             originalVertices.Length + ", Got: " + bakedVertices.Length);
        }
    }

    void LateUpdate()
    {
        // Ensure job is completed by the end of the frame if it hasn't been already
        if (jobScheduled)
        {
            vertexUpdateJobHandle.Complete();
            jobScheduled = false;

            // Apply the deformation
            ApplyDeformation();
        }
    }

    void UpdateWorldVertices()
    {
        // Update the world positions of all vertices based on current transform
        for (int i = 0; i < originalVertices.Length; i++)
        {
            // Calculate the expected world position for original vertices (no deformation)
            currentWorldVertices[i] = transform.TransformPoint(originalVertices[i]);
        }
    }

    void UpdateVertexPhysics(float deltaTime)
    {
        // Create the vertex processing job
        JellyMeshVertexJob vertexJob = new JellyMeshVertexJob
        {
            originalVertices = originalVertices,
            deformedVertices = deformedVertices,
            vertexVelocities = vertexVelocities,
            originalDistances = originalDistances,
            previousWorldVertices = previousWorldVertices,
            currentWorldVertices = currentWorldVertices,
            localToWorldMatrix = transform.localToWorldMatrix,
            worldToLocalMatrix = transform.worldToLocalMatrix,
            localCenterOfMass = localCenterOfMass,

            // Pack physics parameters into float4 for better memory alignment
            physicsParams = new float4(intensity, mass, stiffness, damping),

            // Pack distance settings into float4
            distanceParams = new float4(distanceFalloff, radiusConstraintStrength, 0, 0),
            maintainRadius = maintainRadius ? 1 : 0, // Convert bool to int for jobs

            // Pack response settings into float4
            responseParams = new float4(movementInfluence, falloff, 0, 0),

            // Use the specified time step
            deltaTime = deltaTime
        };

        // Schedule the job with optimized batching
        vertexUpdateJobHandle = vertexJob.Schedule(originalVertices.Length, batchSize);
        jobScheduled = true;

        // Update tracking info
        prevPosition = transform.position;
        prevRotation = transform.rotation;
    }

    // Cache the mesh vertices array to avoid allocating a new array every frame
    private Vector3[] meshVerticesCache;

    void ApplyDeformation()
    {
        // Create the cache array only once
        if (meshVerticesCache == null || meshVerticesCache.Length != deformedVertices.Length)
        {
            meshVerticesCache = new Vector3[deformedVertices.Length];
        }

        // Convert NativeArray<float3> to Vector3[] - using the cached array
        for (int i = 0; i < deformedVertices.Length; i++)
        {
            meshVerticesCache[i] = deformedVertices[i];
        }

        // Apply the deformed vertices to the mesh
        deformableMesh.vertices = meshVerticesCache;

        // Update mesh normals and bounds
        deformableMesh.RecalculateNormals();
        deformableMesh.RecalculateBounds();

        // Update mesh collider if enabled
        if (updateMeshCollider && meshCollider != null)
        {
            // Update collider based on frame interval for performance
            frameCounter = (frameCounter + 1) % colliderUpdateInterval;
            if (frameCounter == 0)
            {
                // Need to temporarily clear the mesh to force Unity to properly update the collider
                Mesh tempMesh = meshCollider.sharedMesh;
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = tempMesh;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        if (!Application.isPlaying || !initialized)
        {
            // If not in play mode, still draw LOD ranges if enabled
            if (useLOD)
            {
                // Quality range
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, minLODDistance);

                // Culling range
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, maxLODDistance);
            }
            return;
        }

        // Ensure job is completed before accessing data for gizmos
        if (jobScheduled)
        {
            vertexUpdateJobHandle.Complete();
            jobScheduled = false;
        }

        // Draw the falloff radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.TransformPoint(localCenterOfMass), falloff);

        // Show center of mass
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(localCenterOfMass), 0.05f);

        // Draw LOD ranges if enabled
        if (useLOD)
        {
            // Quality range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, minLODDistance);

            // Culling range - changed to red for clarity
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, maxLODDistance);

            // Display effect state
            if (isEffectCulled)
            {
                // Draw a red "X" to indicate effect is disabled
                Vector3 position = transform.position + Vector3.up * 0.5f;
                float size = 0.5f;
                Debug.DrawLine(position - Vector3.right * size - Vector3.forward * size,
                               position + Vector3.right * size + Vector3.forward * size, Color.red);
                Debug.DrawLine(position + Vector3.right * size - Vector3.forward * size,
                               position - Vector3.right * size + Vector3.forward * size, Color.red);
            }
        }
    }

    // Reset mesh to original state
    public void ResetMesh()
    {
        if (!initialized) return;

        // Make sure any running job is complete
        if (jobScheduled)
        {
            vertexUpdateJobHandle.Complete();
            jobScheduled = false;
        }

        // Reset vertex positions and velocities
        for (int i = 0; i < originalVertices.Length; i++)
        {
            deformedVertices[i] = originalVertices[i];
            vertexVelocities[i] = float3.zero;
        }

        // Apply the reset
        ApplyDeformation();

        // For skinned mesh, could optionally switch back to original renderer here
        if (useSkinnedMesh && isEffectCulled)
        {
            if (skinnedMeshRenderer != null && meshRenderer != null)
            {
                skinnedMeshRenderer.enabled = true;
                meshRenderer.enabled = false;
            }
        }
    }

    #region Cleanup and Safety Methods

    // Called when Unity is quitting - helps ensure we cleanup resources
    private void OnApplicationQuit()
    {
        CleanupResources();
    }

    void OnDisable()
    {
        // Complete any running jobs when the component is disabled
        if (jobScheduled)
        {
            try
            {
                vertexUpdateJobHandle.Complete();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing job on disable: {e.Message}");
            }
            jobScheduled = false;
        }

        // If using skinned mesh, restore original renderer when disabled
        if (useSkinnedMesh)
        {
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.enabled = true;
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
            }
        }
    }

    void OnEnable()
    {
        // If using skinned mesh, restore our setup when enabled
        if (initialized && useSkinnedMesh && !isEffectCulled)
        {
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.enabled = false;
            }

            if (meshRenderer != null)
            {
                meshRenderer.enabled = true;
            }
        }
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    // Implement IDisposable for more reliable cleanup
    public void Dispose()
    {
        CleanupResources();
        GC.SuppressFinalize(this);
    }

    private void CleanupResources()
    {
        // Safety flag to prevent double disposing
        if (!initialized) return;

        // Make sure any running jobs are complete before disposing resources
        if (jobScheduled)
        {
            try
            {
                vertexUpdateJobHandle.Complete();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing job during cleanup: {e.Message}");
            }
            jobScheduled = false;
        }

        // Properly dispose of all native arrays
        try
        {
            if (originalVertices.IsCreated) originalVertices.Dispose();
            if (deformedVertices.IsCreated) deformedVertices.Dispose();
            if (vertexVelocities.IsCreated) vertexVelocities.Dispose();
            if (originalDistances.IsCreated) originalDistances.Dispose();
            if (previousWorldVertices.IsCreated) previousWorldVertices.Dispose();
            if (currentWorldVertices.IsCreated) currentWorldVertices.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error disposing native arrays: {e.Message}");
        }

        // Clean up temp mesh for skinned mesh rendering
        if (tempBakedMesh != null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(tempBakedMesh);
            else
                DestroyImmediate(tempBakedMesh);
#else
            Destroy(tempBakedMesh);
#endif
            tempBakedMesh = null;
        }

        // Restore original renderer state for skinned mesh
        if (useSkinnedMesh)
        {
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.enabled = true;
            }
        }

        initialized = false;
    }

    #endregion

    // Add editor-specific handling for domain reload
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReload()
    {
        // Find all instances of JellyMeshSystem in the scene and ensure they're cleaned up
        JellyMeshSystem[] instances = FindObjectsOfType<JellyMeshSystem>();
        foreach (var instance in instances)
        {
            try
            {
                instance.CleanupResources();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error cleaning up JellyMeshSystem on domain reload: {e.Message}");
            }
        }
    }
#endif

    // Ensure cleanup happens when exiting play mode
#if UNITY_EDITOR
    private class EditorCleanupHandler
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                JellyMeshSystem[] instances = FindObjectsOfType<JellyMeshSystem>();
                foreach (var instance in instances)
                {
                    try
                    {
                        instance.CleanupResources();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error cleaning up JellyMeshSystem when exiting play mode: {e.Message}");
                    }
                }
            }
        }
    }
#endif


    // Burst-compiled job for vertex physics calculations
    [BurstCompile(FloatPrecision.Medium, FloatMode.Fast, CompileSynchronously = true)]
    struct JellyMeshVertexJob : IJobParallelFor
    {
        // Input data (read-only)
        [ReadOnly] public NativeArray<float3> originalVertices;
        [ReadOnly] public NativeArray<float> originalDistances;
        [ReadOnly] public NativeArray<float3> previousWorldVertices;
        [ReadOnly] public NativeArray<float3> currentWorldVertices;
        [ReadOnly] public Matrix4x4 localToWorldMatrix;
        [ReadOnly] public Matrix4x4 worldToLocalMatrix;
        [ReadOnly] public float3 localCenterOfMass;

        // Both input and output data (simplified approach)
        public NativeArray<float3> deformedVertices;
        public NativeArray<float3> vertexVelocities;

        // Physics parameters - use float4 for better memory alignment
        public float4 physicsParams; // x=intensity, y=mass, z=stiffness, w=damping

        // Distance settings - use float4 for better memory alignment
        public float4 distanceParams; // x=distanceFalloff, y=radiusConstraintStrength, z=unused, w=unused
        public int maintainRadius; // Boolean as int for Burst compatibility

        // Response settings - use float4 for better memory alignment
        public float4 responseParams; // x=movementInfluence, y=falloff, z=unused, w=unused

        // Time settings
        public float deltaTime;

        // Helper method to transform between coordinate systems
        private float3 TransformPointByMatrix(Matrix4x4 matrix, float3 point)
        {
            return new float3(
                matrix.m00 * point.x + matrix.m01 * point.y + matrix.m02 * point.z + matrix.m03,
                matrix.m10 * point.x + matrix.m11 * point.y + matrix.m12 * point.z + matrix.m13,
                matrix.m20 * point.x + matrix.m21 * point.y + matrix.m22 * point.z + matrix.m23
            );
        }

        public void Execute(int i)
        {
            // Unpack parameters for faster access
            float intensity = physicsParams.x;
            float mass = physicsParams.y;
            float stiffness = physicsParams.z;
            float damping = physicsParams.w;

            float distanceFalloff = distanceParams.x;
            float radiusConstraintStrength = distanceParams.y;

            float movementInfluence = responseParams.x;
            float falloff = responseParams.y;

            // Get current vertex data
            float3 vertexPosition = deformedVertices[i];
            float3 vertexVelocity = vertexVelocities[i];

            // Calculate world-space movement
            float3 previousWorldPos = previousWorldVertices[i];
            float3 currentWorldPos = currentWorldVertices[i];
            float3 worldDisplacement = currentWorldPos - previousWorldPos;

            // Distance from center of mass - affects jelly behavior
            float3 relativePos = originalVertices[i] - localCenterOfMass;
            float distFromCenter = math.length(relativePos);

            // Skip vertices at exact center
            if (distFromCenter < 0.001f)
                return;

            // Calculate distance factor (0-1 range, clamped)
            float distanceFactor = math.saturate(distFromCenter / falloff);

            // Convert world displacement to local space for the current orientation
            float3 localDisplacement = TransformPointByMatrix(worldToLocalMatrix, previousWorldPos + worldDisplacement)
                                     - TransformPointByMatrix(worldToLocalMatrix, previousWorldPos);

            // Apply force based on the displacement (unified for both rotation and translation)
            float3 force = localDisplacement * movementInfluence * distanceFactor * intensity;

            // Apply force to velocity
            float inverseMass = 1.0f / mass;
            vertexVelocity += force * inverseMass * deltaTime;

            // Apply spring force to return to original shape
            float3 displacement = vertexPosition - originalVertices[i];
            float stiffnessTimesDelta = stiffness * deltaTime;
            vertexVelocity -= displacement * stiffnessTimesDelta;

            // Apply damping
            float dampingFactor = 1.0f - damping * deltaTime;
            vertexVelocity *= dampingFactor;

            // Update vertex position
            vertexPosition += vertexVelocity * deltaTime;

            // Maintain radius if enabled
            if (maintainRadius != 0 && originalDistances[i] > 0.001f)
            {
                // Get current vertex position relative to center of mass
                float3 currentRelativePos = vertexPosition - localCenterOfMass;
                float currentDist = math.length(currentRelativePos);

                // Only apply if the distance is significantly different
                if (math.abs(currentDist - originalDistances[i]) > 0.001f)
                {
                    // Compute direction
                    float3 direction = currentRelativePos / math.max(0.001f, currentDist);

                    // Get target position at original distance
                    float3 targetPos = localCenterOfMass + (direction * originalDistances[i]);

                    // Calculate blend factor for constraint
                    float blendFactor = radiusConstraintStrength * deltaTime * 10.0f;
                    blendFactor = math.min(blendFactor, 1.0f); // Clamp to avoid over-correction

                    // Blend current position with target position
                    vertexPosition = math.lerp(vertexPosition, targetPos, blendFactor);

                    // Adjust velocity to be more tangential when constraint is strong
                    if (radiusConstraintStrength > 0.5f)
                    {
                        float velocityDotDirection = math.dot(vertexVelocity, direction);
                        float3 radialComponent = direction * velocityDotDirection;
                        vertexVelocity -= radialComponent * radiusConstraintStrength;
                    }
                }
            }

            // Store updated values
            deformedVertices[i] = vertexPosition;
            vertexVelocities[i] = vertexVelocity;
        }
    }
}