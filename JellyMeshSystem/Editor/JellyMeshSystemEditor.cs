using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JellyMeshSystem))]
public class JellyMeshSystemEditor : Editor
{
    // Foldout states
    private bool showPhysicsSettings = true;
    private bool showDistanceSettings = true;
    private bool showResponseSettings = true;
    private bool showColliderSettings = true;
    private bool showPerformanceSettings = true;
    private bool showLODSettings = false;
    private bool showPivotSettings = true;
    private bool showSkinnedMeshSettings = false;
    private bool showHelp = false;

    // SerializedProperties for all variables
    private SerializedProperty intensityProp;
    private SerializedProperty massProp;
    private SerializedProperty stiffnessProp;
    private SerializedProperty dampingProp;

    private SerializedProperty maintainRadiusProp;
    private SerializedProperty distanceFalloffProp;
    private SerializedProperty radiusConstraintStrengthProp;

    private SerializedProperty movementInfluenceProp;
    private SerializedProperty falloffProp;

    private SerializedProperty updateMeshColliderProp;
    private SerializedProperty colliderUpdateIntervalProp;

    private SerializedProperty performanceLevelProp;

    private SerializedProperty useLODProp;
    private SerializedProperty maxLODDistanceProp;
    private SerializedProperty minLODDistanceProp;
    private SerializedProperty showGizmosProp;

    private SerializedProperty customPivotProp;

    private SerializedProperty skinnedMeshUpdateIntervalProp;

    // Help text for each section
    private readonly string physicsHelpText =
        "Intensity: Controls the overall strength of the jelly effect. Higher values create more pronounced deformation.\n\n" +
        "Mass: Affects how quickly vertices respond to forces. Higher mass means slower response but more stability.\n\n" +
        "Stiffness: Controls how strongly vertices return to their original position. Higher values result in more rigid behavior.\n\n" +
        "Damping: Controls how quickly oscillations settle down. Higher values result in quicker stabilization.";

    private readonly string distanceHelpText =
        "Maintain Radius: When enabled, vertices maintain their distance from the pivot point.\n\n" +
        "Distance Falloff: Controls how distance affects the jelly physics. Higher values reduce the effect for distant vertices.\n\n" +
        "Radius Constraint Strength: Controls how strictly vertices maintain their distance from the pivot. Higher values enforce stricter distance constraints.";

    private readonly string responseHelpText =
        "Movement Influence: Controls how strongly the mesh responds to object movement and rotation.\n\n" +
        "Falloff: Controls the radius of influence for physics effects. Vertices beyond this distance are less affected.";

    private readonly string colliderHelpText =
        "Update Mesh Collider: When enabled, the mesh collider will update to match deformations.\n\n" +
        "Collider Update Interval: Controls how often the collider updates. Higher values improve performance but reduce collision accuracy.";

    private readonly string performanceHelpText =
        "Performance Level: Adjust this slider to balance between quality and performance.\n" +
        "0 = Highest quality (updates every frame)\n" +
        "1 = Highest performance (reduced update frequency)";

    private readonly string lodHelpText =
        "Use LOD: Enables automatic Level of Detail based on camera distance.\n\n" +
        "Min LOD Distance: Distance below which full quality is used.\n\n" +
        "Max LOD Distance: Distance at which effect is COMPLETELY DISABLED for maximum performance. The mesh will reset to its original shape when beyond this distance.\n\n" +
        "Show Gizmos: Toggle visualization of LOD ranges and pivot points in the scene view.";

    private readonly string pivotHelpText =
        "Custom Pivot: Optionally specify a custom pivot point for the jelly physics.\n\n" +
        "The pivot must be a child object of this GameObject. If left empty, the center of the mesh will be used as the pivot.\n\n" +
        "You can move the pivot during runtime to dynamically change the center of the jelly effect.";

    private readonly string skinnedMeshHelpText =
        "Skinned Mesh Update Interval: Controls how often the system samples the base skinned mesh animation.\n\n" +
        "Lower values (1-2) provide the most accurate tracking of the animation, but may impact performance.\n\n" +
        "Higher values (5-10) are more performance-friendly but might cause the jelly effect to lag behind complex animations.";

    private void OnEnable()
    {
        // Initialize all serialized properties
        intensityProp = serializedObject.FindProperty("intensity");
        massProp = serializedObject.FindProperty("mass");
        stiffnessProp = serializedObject.FindProperty("stiffness");
        dampingProp = serializedObject.FindProperty("damping");

        maintainRadiusProp = serializedObject.FindProperty("maintainRadius");
        distanceFalloffProp = serializedObject.FindProperty("distanceFalloff");
        radiusConstraintStrengthProp = serializedObject.FindProperty("radiusConstraintStrength");

        movementInfluenceProp = serializedObject.FindProperty("movementInfluence");
        falloffProp = serializedObject.FindProperty("falloff");

        updateMeshColliderProp = serializedObject.FindProperty("updateMeshCollider");
        colliderUpdateIntervalProp = serializedObject.FindProperty("colliderUpdateInterval");

        performanceLevelProp = serializedObject.FindProperty("performanceLevel");

        useLODProp = serializedObject.FindProperty("useLOD");
        maxLODDistanceProp = serializedObject.FindProperty("maxLODDistance");
        minLODDistanceProp = serializedObject.FindProperty("minLODDistance");
        showGizmosProp = serializedObject.FindProperty("showGizmos");

        customPivotProp = serializedObject.FindProperty("customPivot");

        skinnedMeshUpdateIntervalProp = serializedObject.FindProperty("skinnedMeshUpdateInterval");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeaders();

        EditorGUILayout.Space();

        DrawHelpSection();

        EditorGUILayout.Space();

        DrawPhysicsSettings();

        EditorGUILayout.Space();

        DrawDistanceSettings();

        EditorGUILayout.Space();

        DrawResponseSettings();

        EditorGUILayout.Space();

        DrawColliderSettings();

        EditorGUILayout.Space();

        DrawSkinnedMeshSettings();

        EditorGUILayout.Space();

        DrawPerformanceSettings();

        EditorGUILayout.Space();

        DrawLODSettings();

        EditorGUILayout.Space();

        DrawPivotSettings();

        EditorGUILayout.Space();

        DrawResetButton();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeaders()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("JELLY MESH SYSTEM", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("v0.1");
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle kofiButtonStyle = new GUIStyle(GUI.skin.button);
        kofiButtonStyle.normal.textColor = new Color(0.35f, 0.5f, 0.9f); // Ko-fi blue color
        kofiButtonStyle.fontStyle = FontStyle.Bold;

        if (GUILayout.Button("☕ Buy me a coffee", kofiButtonStyle, GUILayout.Height(30), GUILayout.Width(150)))
        {
            Application.OpenURL("https://ko-fi.com/roundy");
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHelpSection()
    {
        showHelp = EditorGUILayout.Foldout(showHelp, "Show Usage Instructions", true, EditorStyles.foldoutHeader);

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "The Jelly Mesh System adds soft-body physics to any mesh in Unity.\n\n" +
                "Quick Start:\n" +
                "1. Ensure your mesh has 'Read/Write Enabled' in its import settings.\n" +
                "2. Works with both regular MeshRenderer and SkinnedMeshRenderer components.\n" +
                "3. Adjust 'Intensity' to control the overall strength of the effect.\n" +
                "4. Adjust 'Stiffness' to control how quickly the mesh returns to its original shape.\n" +
                "5. For better control, add a child object to serve as a custom pivot point.\n\n" +
                "For skinned meshes, check the Skinned Mesh Settings section.",
                MessageType.Info);
        }
    }

    private void DrawPhysicsSettings()
    {
        showPhysicsSettings = EditorGUILayout.Foldout(showPhysicsSettings, "Jelly Physics Settings", true, EditorStyles.foldoutHeader);

        if (showPhysicsSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(intensityProp, new GUIContent("Intensity", "Overall strength of jelly effect"));
            EditorGUILayout.PropertyField(massProp, new GUIContent("Mass", "Higher = more inertia, slower response"));
            EditorGUILayout.PropertyField(stiffnessProp, new GUIContent("Stiffness", "Return to original shape strength"));
            EditorGUILayout.PropertyField(dampingProp, new GUIContent("Damping", "How quickly oscillations settle"));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(physicsHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawSkinnedMeshSettings()
    {
        // Check if this is actually a skinned mesh
        bool isSkinnedMesh = ((JellyMeshSystem)target).GetComponent<SkinnedMeshRenderer>() != null;

        // Create a title with indication if it's applicable
        string title = "Skinned Mesh Settings";
        if (isSkinnedMesh)
        {
            title += " (Active)";
        }
        else
        {
            title += " (Inactive)";
        }

        showSkinnedMeshSettings = EditorGUILayout.Foldout(showSkinnedMeshSettings, title, true, EditorStyles.foldoutHeader);

        if (showSkinnedMeshSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            if (!isSkinnedMesh)
            {
                EditorGUILayout.HelpBox("These settings only apply when used with a SkinnedMeshRenderer component.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!isSkinnedMesh))
            {
                EditorGUILayout.PropertyField(skinnedMeshUpdateIntervalProp, new GUIContent("Update Interval", "How often to sample the animated mesh (frames)"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(skinnedMeshHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawDistanceSettings()
    {
        showDistanceSettings = EditorGUILayout.Foldout(showDistanceSettings, "Distance Settings", true, EditorStyles.foldoutHeader);

        if (showDistanceSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(maintainRadiusProp, new GUIContent("Maintain Radius", "Keep vertices at consistent distance from pivot"));

            using (new EditorGUI.DisabledScope(!maintainRadiusProp.boolValue))
            {
                EditorGUILayout.PropertyField(radiusConstraintStrengthProp, new GUIContent("Radius Constraint Strength", "Strength of radius constraint (0=none, 1=strict)"));
            }

            EditorGUILayout.PropertyField(distanceFalloffProp, new GUIContent("Distance Falloff", "General falloff for distance effects"));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(distanceHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawResponseSettings()
    {
        showResponseSettings = EditorGUILayout.Foldout(showResponseSettings, "Response Settings", true, EditorStyles.foldoutHeader);

        if (showResponseSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(movementInfluenceProp, new GUIContent("Movement Influence", "Movement response strength"));
            EditorGUILayout.PropertyField(falloffProp, new GUIContent("Falloff", "Distance falloff for effects"));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(responseHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawColliderSettings()
    {
        showColliderSettings = EditorGUILayout.Foldout(showColliderSettings, "Collider Settings", true, EditorStyles.foldoutHeader);

        if (showColliderSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(updateMeshColliderProp, new GUIContent("Update Mesh Collider", "Whether to update the mesh collider with deformation"));

            using (new EditorGUI.DisabledScope(!updateMeshColliderProp.boolValue))
            {
                EditorGUILayout.PropertyField(colliderUpdateIntervalProp, new GUIContent("Collider Update Interval", "Update collider every N frames for performance"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(colliderHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPerformanceSettings()
    {
        showPerformanceSettings = EditorGUILayout.Foldout(showPerformanceSettings, "Performance Settings", true, EditorStyles.foldoutHeader);

        if (showPerformanceSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Performance Level (0 = quality, 1 = performance)");
            EditorGUILayout.Slider(performanceLevelProp, 0, 1, new GUIContent(""));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(performanceHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawLODSettings()
    {
        showLODSettings = EditorGUILayout.Foldout(showLODSettings, "LOD Settings", true, EditorStyles.foldoutHeader);

        if (showLODSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(useLODProp, new GUIContent("Use LOD", "Enable automatic LOD based on camera distance"));

            using (new EditorGUI.DisabledScope(!useLODProp.boolValue))
            {
                EditorGUILayout.PropertyField(minLODDistanceProp, new GUIContent("Min LOD Distance", "Distance below which full quality is used"));

                // Use a bold, colored style for max distance to highlight it disables the effect
                GUIStyle boldRedLabel = new GUIStyle(EditorStyles.label);
                boldRedLabel.fontStyle = FontStyle.Bold;

                EditorGUILayout.PropertyField(maxLODDistanceProp, new GUIContent("Max LOD Distance (Culling)", "Distance at which effect is COMPLETELY DISABLED for maximum performance"));

                EditorGUILayout.HelpBox("Beyond the Max LOD Distance, the jelly effect will be completely disabled and the mesh will return to its original shape.", MessageType.Info);
            }

            EditorGUILayout.PropertyField(showGizmosProp, new GUIContent("Show Gizmos", "Toggle visualization of LOD ranges and pivot points"));

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lodHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPivotSettings()
    {
        showPivotSettings = EditorGUILayout.Foldout(showPivotSettings, "Custom Pivot", true, EditorStyles.foldoutHeader);

        if (showPivotSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(customPivotProp, new GUIContent("Custom Pivot", "Optional custom pivot point (must be a child of this object)"));

            EditorGUILayout.Space();

            // Check if pivot is not a child object
            var jellyMesh = (JellyMeshSystem)target;
            if (jellyMesh.customPivot != null && !IsChildOf(jellyMesh.transform, jellyMesh.customPivot))
            {
                EditorGUILayout.HelpBox("Warning: Custom pivot must be a child of this object for proper behavior.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(pivotHelpText, MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawResetButton()
    {
        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Reset Mesh", GUILayout.Height(30)))
            {
                var jellyMesh = (JellyMeshSystem)target;
                jellyMesh.ResetMesh();
            }
        }
    }

    private bool IsChildOf(Transform parent, Transform child)
    {
        if (child.parent == parent)
            return true;

        if (child.parent == null)
            return false;

        return IsChildOf(parent, child.parent);
    }
}