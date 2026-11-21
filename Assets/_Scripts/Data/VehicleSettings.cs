using System;
using UnityEngine;

[CreateAssetMenu(fileName = "VehiclesSettings", menuName = "Game/Vehicles Settings")]
public class VehiclesSettings : ScriptableObject
{
    [Header("Общие для всех машин")]
    public SharedVehicleSettings shared = new SharedVehicleSettings();

    [Header("Игрок")]
    public PlayerVehicleSettings player = new PlayerVehicleSettings();

    [Header("Боты")]
    public BotVehicleSettings bot = new BotVehicleSettings();
}

[Serializable]
public class GearSettings
{
    [Tooltip("До какой скорости (км/ч) тянет эта передача")]
    public float maxSpeed = 30f;

    [Tooltip("Ускорение на этой передаче (м/с²)")]
    public float acceleration = 1.0f;

    [Tooltip("Множитель тяги на этой передаче (1 = стандарт)")]
    public float powerMultiplier = 1f;
}

[Serializable]
public class SharedVehicleSettings
{
    [Header("Основные параметры")]
    [Range(10, 500)] public int maxReverseSpeed = 45;
    [Tooltip("Сколько колёс ведущих: 4 = полный привод, 2 = передний/задний")]
    [Range(1, 4)] public int drivenWheels = 4;
    [Range(10, 45)] public int maxSteeringAngle = 27;
    [Range(100, 1000)] public int brakeForce = 350;
    public Vector3 bodyMassCenter;

    [Header("Руль и дрифт")]
    public float steerSpeedRate = 10f;
    public float wheelTurnRate = 20f;
    [Range(1, 20)] public int handbrakeDriftMultiplier = 5;
    public float handbrakeBrakeMultiplier = 2f;
    public float driftRecoveryTime = 1.5f;

    [Header("Двигатель и КПП")]
    public bool useGearbox = true;
    [Range(500, 1500)] public float idleRPM = 800f;
    [Range(3000, 9000)] public float maxRPM = 6500f;
    [Range(0f, 1f)] public float shiftTime = 0.25f;
    [Range(0f, 1f)] public float torqueWhileShifting = 0.5f;
    [Range(0, 2000)] public float engineBrakeForce = 600f;
}

[Serializable]
public class VehicleTuning
{
    [Header("Коробка передач (список передач по порядку)")]
    public GearSettings[] gears = new GearSettings[]
    {
        new GearSettings { maxSpeed = 20f, acceleration = 1.4f, powerMultiplier = 1f },
        new GearSettings { maxSpeed = 40f, acceleration = 1.1f, powerMultiplier = 1f },
        new GearSettings { maxSpeed = 65f, acceleration = 0.8f, powerMultiplier = 1f },
        new GearSettings { maxSpeed = 85f, acceleration = 0.5f, powerMultiplier = 1f },
        new GearSettings { maxSpeed = 105f, acceleration = 0.3f, powerMultiplier = 1f }
    };

    [Header("Общая управляемость")]
    [Range(0.1f, 5f)] public float steeringSpeed = 0.5f;
    [Range(0.1f, 5f)] public float globalPowerMultiplier = 1f;
    [Tooltip("Ускорение заднего хода (м/с²)")]
    public float reverseAcceleration = 0.9f;

    [Header("Эффекты и звук")]
    public bool useEffects = true;
    public bool useSounds = true;

    public float MaxForwardSpeed =>
        gears != null && gears.Length > 0 ? gears[gears.Length - 1].maxSpeed : 90f;
}

[Serializable]
public class PlayerVehicleSettings
{
    public VehicleTuning tuning = new VehicleTuning();

    [Header("Интерфейс и управление")]
    public bool useUI = true;
    public bool useTouchControls = false;
}

[Serializable]
public class BotVehicleSettings
{
    public VehicleTuning tuning = new VehicleTuning();
    public BotAISettings ai = new BotAISettings();
}

[Serializable]
public class PersonalitySettings
{
    public bool canBullyHunt = false;
    public bool allowNitro = true;
    public float nitroMultiplier = 1.35f;
    [Range(0f, 1f)] public float nitroSpeedFraction = 0.95f;
    public float laneChangeSpeed = 5f;
    [Range(0.05f, 1f)] public float cornerBrakeStrength = 0.45f;
    public float sideLaneBonus = 0f;

    public static PersonalitySettings CreateBully() => new PersonalitySettings
    {
        canBullyHunt = true,
        allowNitro = true,
        nitroMultiplier = 1.65f,
        nitroSpeedFraction = 0.85f,
        laneChangeSpeed = 8f,
        cornerBrakeStrength = 0.6f,
        sideLaneBonus = 0f
    };

    public static PersonalitySettings CreateCautious() => new PersonalitySettings
    {
        canBullyHunt = false,
        allowNitro = false,
        nitroMultiplier = 1.15f,
        nitroSpeedFraction = 0.95f,
        laneChangeSpeed = 2.5f,
        cornerBrakeStrength = 0.35f,
        sideLaneBonus = 5f
    };

    public static PersonalitySettings CreateStrategist() => new PersonalitySettings
    {
        canBullyHunt = false,
        allowNitro = true,
        nitroMultiplier = 1.35f,
        nitroSpeedFraction = 0.95f,
        laneChangeSpeed = 5f,
        cornerBrakeStrength = 0.45f,
        sideLaneBonus = 0f
    };
}

[Serializable]
public class BotAISettings
{
    [Header("Характеры")]
    public PersonalitySettings bully = PersonalitySettings.CreateBully();
    public PersonalitySettings cautious = PersonalitySettings.CreateCautious();
    public PersonalitySettings strategist = PersonalitySettings.CreateStrategist();

    [Header("Дорога и полосы")]
    public float laneOffsetDistance = 5f;
    public float maxRoadBoundary = 15f;
    public float emergencyReturnSpeed = 6f;
    [Range(0f, 1f)] public float laneChangeMidpoint = 0.5f;

    [Header("Тайминги")]
    public float startAccelerationTime = 3f;
    public float decisionInterval = 0.15f;
    public float laneDecisionCooldown = 2f;

    [Header("Скорость и повороты")]
    public float baseMaxSpeed = 120f;
    public float physicalBrakePower = 45f;
    public float brakeOvershootTolerance = 1f;
    public float brakeForceMultiplier = 1.5f;
    public float baseLookAhead = 22f;
    public float speedLookAheadFactor = 0.15f;
    public float cornerCheckDistance = 40f;
    public int cornerCheckSteps = 10;
    public float cornerSharpnessDot = 0.94f;
    public float cornerMinDot = -0.3f;

    [Header("Сцепление в поворотах")]
    [Range(0f, 1f)] public float gripAlignSpeedFraction = 0.7f;
    public float gripAlignMinSqrSpeed = 25f;
    public float gripAlignRate = 3f;

    [Header("Руление и препятствия")]
    public float predictionFactor = 0.15f;
    public float steerDeadzone = 1.5f;
    public float obstacleCastOriginHeight = 0.5f;
    public float obstacleDetectRadius = 1.8f;
    public float obstacleDetectDistance = 22f;
    public float laneCheckRadius = 1.5f;
    public float laneCheckDistance = 15f;
    public float laneBaseScore = 10f;
    public float laneBlockedPenalty = 20f;

    [Header("Таран (Задира)")]
    public float bullySearchRadius = 30f;
    public float bullyMinTargetOffset = 4f;
    public float bullyBoundaryMarginSelf = 2.5f;
    public float bullyBoundaryMarginTarget = 1f;
    public float bullyCooldown = 4f;

    [Header("Аварии и переворот")]
    public float crashImpactThreshold = 12f;
    public float crashMaxImpactMultiplier = 3f;
    [Range(0f, 1f)] public float groundNormalMinY = 0.7f;
    public float launchUpSpeedMin = 3f;
    public float launchUpSpeedMax = 9f;
    public float launchTumbleSpeed = 4f;
    public float launchYawSpeed = 3f;
    public float crashRecoveryTime = 1.2f;
    public float crashRecoveryMinFactor = 0.5f;
    public float crashCooldown = 0.6f;
    public float groundCheckDistance = 1.5f;
    public float groundCheckOriginHeight = 0.5f;
    public float flipRecoveryTime = 2f;
    public float flipUpDot = 0.5f;
    public float flipMaxSqrSpeed = 4f;
    public float flipLiftHeight = 1f;
}