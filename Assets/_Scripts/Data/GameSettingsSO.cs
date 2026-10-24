using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Configs/GameSettings")]
public class GameSettingsSO : ScriptableObject
{
    public CameraSettings Camera = new();
    public PlayerSettings Player = new();
    public ChickenSettings Chicken = new();
}

[Serializable]
public class CameraSettings
{

    // --- Пешком ---
    public float Sensitivity = 2f;
    public float Distance = 3f;
    public Vector3 TargetOffset = new Vector3(0, 1.5f, 0);
    public Vector2 VerticalLimits = new Vector2(-45f, 45f);
    public KeyCode Key = KeyCode.V;
    public CameraMode Mode = CameraMode.ThirdPerson;
    public float MaxDistance = 10f;
    public float MinDistance = 0.01f;

    // --- В транспорте (отдельный блок) ---
    public VehicleCameraSettings Vehicle = new();
}
[Serializable]
public class VehicleCameraSettings
{
    public float Distance = 6f;
    public Vector3 TargetOffset = new Vector3(0f, 1f, 0f);
    public Vector2 VerticalLimits = new Vector2(-20f, 60f);
    public float TransitionSpeed = 5f; // чем больше, тем быстрее переход
}

[Serializable]
public class PlayerSettings
{
    public float Speed = 10f;
    public float Gravity = -9.81f;
    [Min(0f)] public float JumpHeight = 1.5f;

    [Header("Jump Timing")]
    [Tooltip("Задержка между нажатием пробела и отрывом от земли (время на приседание в анимации), сек")]
    [Min(0f)] public float JumpDelay = 0.25f;

    [Tooltip("Стоять на месте, пока идёт приседание перед прыжком")]
    public bool LockMoveWhileCrouch = true;

    [Tooltip("Сколько секунд после касания земли держится анимация прыжка (амортизация при приземлении)")]
    [Min(0f)] public float LandingDelay = 0.2f;

    [Tooltip("Стоять на месте, пока идёт амортизация после приземления")]
    public bool LockMoveWhileLanding = true;

    [Header("Animator")]
    public string AnimKeyIdle = "isIdle";
    public string AnimKeyWalk = "isWalk";
    public string AnimKeyRun = "isRun";
    public string AnimKeyJump = "isJump";

    [Tooltip("Имя состояния прыжка в Animator. Пока оно играется, ходьба и бег ждут. Если пусто, ждём только приземления и LandingDelay")]
    public string JumpStateName = "Jump";
}
[System.Serializable]
public struct SpeedProfileSettings
{
    [Header("Acceleration Range (a) [Min, Max]")]
    public Vector2 ARange;

    [Header("Initial Speed Range (b) [Min, Max]")]
    public Vector2 BRange;

    [Min(0f)]
    public float MaxSpeed;

    public SpeedProfileSettings(Vector2 aRange, Vector2 bRange, float maxSpeed)
    {
        ARange = aRange;
        BRange = bRange;
        MaxSpeed = maxSpeed;
    }
}
[System.Serializable]
public class ChickenSettings
{
    [Header("Movement Core")]
    [Min(0f)] public float TurnSpeed = 8f;
    public float Gravity = -9.81f;
    public float GroundedStickyVelocity = -2f;
    public float DirectionEpsilon = 0.0001f;

    [Header("Individual Random Variations")]
    [Tooltip("Разброс дистанции реакции (+-5 метров по умолчанию)")]
    public float DistanceVariance = 5f;

    [Tooltip("Максимальный угол отклонения от прямой траектории при побеге (в градусах)")]
    [Range(0f, 45f)] public float FleeAngleVariance = 20f;

    [Tooltip("Множитель общей физической формы курицы [Min, Max]")]
    public Vector2 IndividualSpeedMultiplierRange = new(0.85f, 1.15f);

    [Header("Speed Profiles V(t) = a*t + b")]
    public SpeedProfileSettings WalkProfile = new(
        aRange: new Vector2(0.1f, 0.4f),
        bRange: new Vector2(0.8f, 1.5f),
        maxSpeed: 2.5f
    );

    public SpeedProfileSettings FleeProfile = new(
        aRange: new Vector2(1.0f, 2.5f),
        bRange: new Vector2(3.0f, 4.5f),
        maxSpeed: 8.0f
    );

    public SpeedProfileSettings SeekFoodProfile = new(
        aRange: new Vector2(0.3f, 0.8f),
        bRange: new Vector2(1.5f, 2.2f),
        maxSpeed: 4.0f
    );

    [Header("Detection Base")]
    [Min(0f)] public float FleeDistance = 5f;
    [Min(0f)] public float CalmDistance = 7f;

    [Header("State Timers (Min, Max)")]
    public Vector2 EatingTimeRange = new(2f, 5f);
    public Vector2 StandingTimeRange = new(1.5f, 3f);
    public Vector2 WalkingTimeRange = new(3f, 6f);
}
public enum CameraMode
{
    FirstPerson,
    ThirdPerson
}