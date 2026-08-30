using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PrometeoCarController))]
public class PrometeoEditor : Editor
{
    private SerializedObject SO;

    // CAR SETUP
    private SerializedProperty isAi;
    private SerializedProperty maxSpeed;
    private SerializedProperty maxReverseSpeed;
    private SerializedProperty accelerationMultiplier;
    private SerializedProperty maxSteeringAngle;
    private SerializedProperty steeringSpeed;
    private SerializedProperty brakeForce;
    private SerializedProperty decelerationMultiplier;
    private SerializedProperty handbrakeDriftMultiplier;
    private SerializedProperty bodyMassCenter;

    // WHEELS VARIABLES
    private SerializedProperty frontLeftMesh;
    private SerializedProperty frontLeftCollider;
    private SerializedProperty frontRightMesh;
    private SerializedProperty frontRightCollider;
    private SerializedProperty rearLeftMesh;
    private SerializedProperty rearLeftCollider;
    private SerializedProperty rearRightMesh;
    private SerializedProperty rearRightCollider;

    // PARTICLE SYSTEMS
    private SerializedProperty useEffects;
    private SerializedProperty RLWParticleSystem;
    private SerializedProperty RRWParticleSystem;
    private SerializedProperty RLWTireSkid;
    private SerializedProperty RRWTireSkid;

    // UI
    private SerializedProperty useUI;
    private SerializedProperty carSpeedText;

    // SOUNDS
    private SerializedProperty useSounds;
    private SerializedProperty carEngineSound;
    private SerializedProperty tireScreechSound;

    // TOUCH CONTROLS
    private SerializedProperty useTouchControls;
    private SerializedProperty throttleButton;
    private SerializedProperty reverseButton;
    private SerializedProperty turnRightButton;
    private SerializedProperty turnLeftButton;
    private SerializedProperty handbrakeButton;

    private void OnEnable()
    {
        SO = serializedObject;

        isAi = SO.FindProperty("isAi");
        maxSpeed = SO.FindProperty("maxSpeed");
        maxReverseSpeed = SO.FindProperty("maxReverseSpeed");
        accelerationMultiplier = SO.FindProperty("accelerationMultiplier");
        maxSteeringAngle = SO.FindProperty("maxSteeringAngle");
        steeringSpeed = SO.FindProperty("steeringSpeed");
        brakeForce = SO.FindProperty("brakeForce");
        decelerationMultiplier = SO.FindProperty("decelerationMultiplier");
        handbrakeDriftMultiplier = SO.FindProperty("handbrakeDriftMultiplier");
        bodyMassCenter = SO.FindProperty("bodyMassCenter");

        frontLeftMesh = SO.FindProperty("frontLeftMesh");
        frontLeftCollider = SO.FindProperty("frontLeftCollider");
        frontRightMesh = SO.FindProperty("frontRightMesh");
        frontRightCollider = SO.FindProperty("frontRightCollider");
        rearLeftMesh = SO.FindProperty("rearLeftMesh");
        rearLeftCollider = SO.FindProperty("rearLeftCollider");
        rearRightMesh = SO.FindProperty("rearRightMesh");
        rearRightCollider = SO.FindProperty("rearRightCollider");

        useEffects = SO.FindProperty("useEffects");
        RLWParticleSystem = SO.FindProperty("RLWParticleSystem");
        RRWParticleSystem = SO.FindProperty("RRWParticleSystem");
        RLWTireSkid = SO.FindProperty("RLWTireSkid");
        RRWTireSkid = SO.FindProperty("RRWTireSkid");

        useUI = SO.FindProperty("useUI");
        carSpeedText = SO.FindProperty("carSpeedText");

        useSounds = SO.FindProperty("useSounds");
        carEngineSound = SO.FindProperty("carEngineSound");
        tireScreechSound = SO.FindProperty("tireScreechSound");

        useTouchControls = SO.FindProperty("useTouchControls");
        throttleButton = SO.FindProperty("throttleButton");
        reverseButton = SO.FindProperty("reverseButton");
        turnRightButton = SO.FindProperty("turnRightButton");
        turnLeftButton = SO.FindProperty("turnLeftButton");
        handbrakeButton = SO.FindProperty("handbrakeButton");
    }

    public override void OnInspectorGUI()
    {
        SO.Update();

        // -------------------------------------------------------------
        // CAR SETUP
        // -------------------------------------------------------------
        GUILayout.Space(15);
        GUILayout.Label("CAR SETUP", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.PropertyField(isAi, new GUIContent("Is AI?"));
        EditorGUILayout.IntSlider(maxSpeed, 20, 190, new GUIContent("Max Speed:"));
        EditorGUILayout.IntSlider(maxReverseSpeed, 10, 120, new GUIContent("Max Reverse Speed:"));
        EditorGUILayout.IntSlider(accelerationMultiplier, 1, 10, new GUIContent("Acceleration Multiplier:"));
        EditorGUILayout.IntSlider(maxSteeringAngle, 10, 45, new GUIContent("Max Steering Angle:"));
        EditorGUILayout.Slider(steeringSpeed, 0.1f, 1f, new GUIContent("Steering Speed:"));
        EditorGUILayout.IntSlider(brakeForce, 100, 600, new GUIContent("Brake Force:"));
        EditorGUILayout.IntSlider(decelerationMultiplier, 1, 10, new GUIContent("Deceleration Multiplier:"));
        EditorGUILayout.IntSlider(handbrakeDriftMultiplier, 1, 10, new GUIContent("Drift Multiplier:"));
        EditorGUILayout.PropertyField(bodyMassCenter, new GUIContent("Mass Center of Car:"));

        // -------------------------------------------------------------
        // WHEELS
        // -------------------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("WHEELS", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.PropertyField(frontLeftMesh, new GUIContent("Front Left Mesh:"));
        EditorGUILayout.PropertyField(frontLeftCollider, new GUIContent("Front Left Collider:"));

        EditorGUILayout.PropertyField(frontRightMesh, new GUIContent("Front Right Mesh:"));
        EditorGUILayout.PropertyField(frontRightCollider, new GUIContent("Front Right Collider:"));

        EditorGUILayout.PropertyField(rearLeftMesh, new GUIContent("Rear Left Mesh:"));
        EditorGUILayout.PropertyField(rearLeftCollider, new GUIContent("Rear Left Collider:"));

        EditorGUILayout.PropertyField(rearRightMesh, new GUIContent("Rear Right Mesh:"));
        EditorGUILayout.PropertyField(rearRightCollider, new GUIContent("Rear Right Collider:"));

        // -------------------------------------------------------------
        // EFFECTS
        // -------------------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("EFFECTS", EditorStyles.boldLabel);
        GUILayout.Space(5);

        useEffects.boolValue = EditorGUILayout.BeginToggleGroup("Use effects (particle systems)?", useEffects.boolValue);
        GUILayout.Space(5);
        EditorGUILayout.PropertyField(RLWParticleSystem, new GUIContent("Rear Left Particle System:"));
        EditorGUILayout.PropertyField(RRWParticleSystem, new GUIContent("Rear Right Particle System:"));
        EditorGUILayout.PropertyField(RLWTireSkid, new GUIContent("Rear Left Trail Renderer:"));
        EditorGUILayout.PropertyField(RRWTireSkid, new GUIContent("Rear Right Trail Renderer:"));
        EditorGUILayout.EndToggleGroup();

        // -------------------------------------------------------------
        // UI
        // -------------------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("UI", EditorStyles.boldLabel);
        GUILayout.Space(5);

        useUI.boolValue = EditorGUILayout.BeginToggleGroup("Use UI (Speed text)?", useUI.boolValue);
        GUILayout.Space(5);
        EditorGUILayout.PropertyField(carSpeedText, new GUIContent("Speed Text (UI):"));
        EditorGUILayout.EndToggleGroup();

        // -------------------------------------------------------------
        // SOUNDS
        // -------------------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("SOUNDS", EditorStyles.boldLabel);
        GUILayout.Space(5);

        useSounds.boolValue = EditorGUILayout.BeginToggleGroup("Use sounds (car sounds)?", useSounds.boolValue);
        GUILayout.Space(5);
        EditorGUILayout.PropertyField(carEngineSound, new GUIContent("Car Engine Sound:"));
        EditorGUILayout.PropertyField(tireScreechSound, new GUIContent("Tire Screech Sound:"));
        EditorGUILayout.EndToggleGroup();

        // -------------------------------------------------------------
        // TOUCH CONTROLS
        // -------------------------------------------------------------
        GUILayout.Space(20);
        GUILayout.Label("TOUCH CONTROLS", EditorStyles.boldLabel);
        GUILayout.Space(5);

        useTouchControls.boolValue = EditorGUILayout.BeginToggleGroup("Use touch controls (mobile devices)?", useTouchControls.boolValue);
        GUILayout.Space(5);
        EditorGUILayout.PropertyField(throttleButton, new GUIContent("Throttle Button:"));
        EditorGUILayout.PropertyField(reverseButton, new GUIContent("Brakes/Reverse Button:"));
        EditorGUILayout.PropertyField(turnLeftButton, new GUIContent("Turn Left Button:"));
        EditorGUILayout.PropertyField(turnRightButton, new GUIContent("Turn Right Button:"));
        EditorGUILayout.PropertyField(handbrakeButton, new GUIContent("Handbrake Button:"));
        EditorGUILayout.EndToggleGroup();

        // Единый вызов сохранения в самом конце
        GUILayout.Space(10);
        SO.ApplyModifiedProperties();
    }
}