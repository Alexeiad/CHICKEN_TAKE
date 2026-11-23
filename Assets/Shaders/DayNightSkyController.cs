using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SkyCycleController : MonoBehaviour
{
    [Serializable]
    public struct SkyState
    {
        public string name;

        [Header("Небо, горизонт и свет")]
        public Color skyTop;
        public Color skyHorizon;
        public Color horizonGlowColor;
        [Range(0f, 2.5f)] public float horizonGlowStrength;
        public Color lightColor;
        [Min(0f)] public float lightIntensity;

        [Header("Облака")]
        public Color cloudBright;
        public Color cloudDark;
        [Range(0f, 1f)] public float cloudCoverage;
        [Range(0f, 1f)] public float cloudSunLight;

        [Header("Солнце, звёзды и блики")]
        public Color sunColor;
        [Range(0f, 2.5f)] public float sunCoronaStrength;
        [Range(0f, 2.5f)] public float sunFXStrength;
        [Range(0f, 2.5f)] public float glareStrength;
        [Range(0f, 2.0f)] public float raysStrength;
        [Range(0f, 1.0f)] public float starsVisibility;
    }

    [Header("Ссылки")]
    public Material skyboxMaterial;
    public Light sunLight;

    [Header("Время")]
    [Min(0f)] public float timeMultiplier = 1f;
    [Min(10f)] public float fullDaySeconds = 120f;
    [Range(0f, 1f)] public float startTimeOfDay = 0.22f;
    public bool animateInEditMode = true;

    [Header("Доля дня и сумерки")]
    [Range(0.02f, 0.15f)] public float eveningLead = 0.07f;
    [Range(0.01f, 0.12f)] public float nightLead = 0.05f;
    [Range(0.2f, 15f)] public float transitionSmoothness = 1.8f;

    [Header("Орбита")]
    [Range(-60f, 60f)] public float orbitTilt = 22f;
    [Range(0f, 360f)] public float orbitYaw = 0f;

    [Header("Освещение сцены (чтобы тени не были чёрными)")]
    [Range(0.15f, 1.5f)] public float minLightIntensity = 0.55f;   // минимальная яркость света
    [Range(0.0f, 1.5f)] public float ambientIntensity = 0.85f;    // сила ambient
    public Color ambientDay = new Color(0.55f, 0.62f, 0.75f);
    public Color ambientEvening = new Color(0.55f, 0.38f, 0.28f);
    public Color ambientNight = new Color(0.18f, 0.22f, 0.35f);

    [Header("Общие настройки")]
    [Range(0.002f, 0.06f)] public float sunSize = 0.011f;
    [Range(0f, 2.0f)] public float sunHorizonBoost = 0.65f;
    [Range(0.006f, 0.09f)] public float moonSize = 0.024f;
    [Range(0f, 1.5f)] public float moonHaloStrength = 0.35f;
    [Range(0.35f, 4.0f)] public float cloudScale = 0.72f;
    public Vector2 cloudSpeed = new Vector2(0.06f, 0.03f);
    [Range(0f, 1.5f)] public float cloudWarp = 1.15f;
    [Range(0f, 1.5f)] public float cloudShadowStrength = 0.40f;
    [Range(0f, 1f)] public float cloudAmbient = 0.45f;
    [Range(0f, 2.0f)] public float colorShiftSpeed = 0.22f;
    [Range(0f, 0.25f)] public float colorShiftAmount = 0.06f;

    [Header("Состояния")]
    public SkyState dayState = new SkyState
    {
        name = "Day",
        skyTop = new Color(0.14f, 0.46f, 0.92f),
        skyHorizon = new Color(0.68f, 0.86f, 0.99f),
        horizonGlowColor = new Color(1.00f, 0.82f, 0.52f),
        horizonGlowStrength = 0.35f,
        lightColor = new Color(1.00f, 0.96f, 0.85f),
        lightIntensity = 1.35f,
        cloudBright = new Color(1.00f, 0.99f, 0.97f),
        cloudDark = new Color(0.48f, 0.52f, 0.61f),
        cloudCoverage = 0.70f,
        cloudSunLight = 1.00f,
        sunColor = new Color(1.00f, 0.96f, 0.85f),
        sunCoronaStrength = 0.85f,
        sunFXStrength = 1.00f,
        glareStrength = 0.75f,
        raysStrength = 0.65f,
        starsVisibility = 0.00f
    };

    public SkyState eveningState = new SkyState
    {
        name = "Evening",
        skyTop = new Color(0.15f, 0.19f, 0.44f),
        skyHorizon = new Color(1.00f, 0.45f, 0.15f),
        horizonGlowColor = new Color(1.00f, 0.38f, 0.10f),
        horizonGlowStrength = 1.35f,
        lightColor = new Color(1.00f, 0.55f, 0.28f),
        lightIntensity = 0.95f,
        cloudBright = new Color(1.00f, 0.72f, 0.44f),
        cloudDark = new Color(0.40f, 0.34f, 0.42f),
        cloudCoverage = 0.74f,
        cloudSunLight = 0.90f,
        sunColor = new Color(1.00f, 0.84f, 0.55f),
        sunCoronaStrength = 1.15f,
        sunFXStrength = 1.20f,
        glareStrength = 0.95f,
        raysStrength = 0.85f,
        starsVisibility = 0.15f
    };

    public SkyState nightState = new SkyState
    {
        name = "Night",
        skyTop = new Color(0.010f, 0.018f, 0.050f),
        skyHorizon = new Color(0.045f, 0.075f, 0.155f),
        horizonGlowColor = new Color(0.14f, 0.24f, 0.48f),
        horizonGlowStrength = 0.20f,
        lightColor = new Color(0.45f, 0.55f, 0.85f),
        lightIntensity = 0.45f,          // было слишком низко
        cloudBright = new Color(0.30f, 0.36f, 0.52f),
        cloudDark = new Color(0.14f, 0.16f, 0.24f),
        cloudCoverage = 0.58f,
        cloudSunLight = 0.00f,
        sunColor = new Color(1.00f, 0.96f, 0.85f),
        sunCoronaStrength = 0.00f,
        sunFXStrength = 0.00f,
        glareStrength = 0.00f,
        raysStrength = 0.00f,
        starsVisibility = 1.00f
    };

    [Header("Отладка")]
    [Range(0f, 1f)] public float timeOfDay;
    [Range(0f, 360f)] public float currentOrbitAngleDeg;
    public float simTime;
    public string currentStateName;

    private static Texture2D _cachedMoonTex;
    private SkyState _currentSwitchedState;
    private bool _stateInitialized;
    private bool _inited;
    private double _lastEditorTime;
    private bool _hasEditorTime;

    // Shader property IDs
    private static readonly int SkyTopId = Shader.PropertyToID("_SkyTop");
    private static readonly int SkyHorizonId = Shader.PropertyToID("_SkyHorizon");
    private static readonly int HorizonGlowColorId = Shader.PropertyToID("_HorizonGlowColor");
    private static readonly int HorizonGlowStrengthId = Shader.PropertyToID("_HorizonGlowStrength");
    private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
    private static readonly int SunHorizonBoostId = Shader.PropertyToID("_SunHorizonBoost");
    private static readonly int SunCoronaStrengthId = Shader.PropertyToID("_SunCoronaStrength");
    private static readonly int SunFXStrengthId = Shader.PropertyToID("_SunFXStrength");
    private static readonly int RaysStrengthId = Shader.PropertyToID("_RaysStrength");
    private static readonly int GlareStrengthId = Shader.PropertyToID("_GlareStrength");
    private static readonly int MoonTexId = Shader.PropertyToID("_MoonTex");
    private static readonly int MoonSizeId = Shader.PropertyToID("_MoonSize");
    private static readonly int MoonHaloStrengthId = Shader.PropertyToID("_MoonHaloStrength");
    private static readonly int StarsVisibilityId = Shader.PropertyToID("_StarsVisibility");
    private static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
    private static readonly int CloudSpeedId = Shader.PropertyToID("_CloudSpeed");
    private static readonly int CloudWarpId = Shader.PropertyToID("_CloudWarp");
    private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
    private static readonly int CloudBrightId = Shader.PropertyToID("_CloudBright");
    private static readonly int CloudDarkId = Shader.PropertyToID("_CloudDark");
    private static readonly int CloudSunLightId = Shader.PropertyToID("_CloudSunLight");
    private static readonly int CloudShadowStrengthId = Shader.PropertyToID("_CloudShadowStrength");
    private static readonly int CloudAmbientId = Shader.PropertyToID("_CloudAmbient");
    private static readonly int SimTimeId = Shader.PropertyToID("_SimTime");
    private static readonly int ColorShiftSpeedId = Shader.PropertyToID("_ColorShiftSpeed");
    private static readonly int ColorShiftAmountId = Shader.PropertyToID("_ColorShiftAmount");
    private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    private static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
    private static readonly int HasScriptDataId = Shader.PropertyToID("_HasScriptData");

    void OnEnable()
    {
        if (sunLight == null) sunLight = GetComponent<Light>();
        if (skyboxMaterial == null && RenderSettings.skybox != null)
            skyboxMaterial = RenderSettings.skybox;

        if (_cachedMoonTex == null)
            _cachedMoonTex = GenerateMoonTexture(256);

        if (!_inited)
        {
            timeOfDay = Mathf.Repeat(startTimeOfDay, 1f);
            _inited = true;
        }

        if (!_stateInitialized)
        {
            _currentSwitchedState = dayState;
            _stateInitialized = true;
        }

        // Важно: включаем нормальный ambient
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        _hasEditorTime = false;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.update += OnEditorUpdate;
#endif
        UpdateCycle(0f, true);
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= OnEditorUpdate;
#endif
        _hasEditorTime = false;
    }

#if UNITY_EDITOR
    private void OnEditorUpdate()
    {
        if (Application.isPlaying || this == null) return;

        double now = EditorApplication.timeSinceStartup;
        if (!_hasEditorTime)
        {
            _lastEditorTime = now;
            _hasEditorTime = true;
            return;
        }

        float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, 0.1f);
        _lastEditorTime = now;

        if (!animateInEditMode) return;

        UpdateCycle(dt, false);
        SceneView.RepaintAll();
    }
#endif

    void Update()
    {
        if (!Application.isPlaying) return;
        UpdateCycle(Mathf.Clamp(Time.deltaTime, 0f, 0.1f), false);
    }

    void OnValidate()
    {
        if (sunLight == null) sunLight = GetComponent<Light>();
        if (_cachedMoonTex == null) _cachedMoonTex = GenerateMoonTexture(256);
        UpdateCycle(0f, true);
    }

    private void UpdateCycle(float rawDt, bool snapState)
    {
        float dt = rawDt * Mathf.Max(0f, timeMultiplier);
        simTime += dt;

        float fullSec = Mathf.Max(10f, fullDaySeconds);
        timeOfDay = Mathf.Repeat(timeOfDay + dt / fullSec, 1f);

        currentOrbitAngleDeg = timeOfDay * 360f;
        float rad = currentOrbitAngleDeg * Mathf.Deg2Rad;

        Vector3 baseSun = new Vector3(0f, Mathf.Sin(rad), Mathf.Cos(rad));
        Quaternion orbitRot = Quaternion.Euler(orbitTilt, orbitYaw, 0f);
        Vector3 toSun = (orbitRot * baseSun).normalized;
        Vector3 toMoon = (-toSun).normalized;

        SkyState targetState = PickTargetState(toSun.y);
        currentStateName = targetState.name;

        if (snapState || rawDt <= 0f)
            _currentSwitchedState = targetState;
        else
        {
            float step = transitionSmoothness * rawDt;
            _currentSwitchedState = StepTowardState(_currentSwitchedState, targetState, step);
        }

        if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
            RenderSettings.skybox = skyboxMaterial;

        // === Освещение сцены ===
        if (sunLight != null)
        {
            // Направление
            Vector3 activeDir = toSun.y >= -0.05f ? toSun : toMoon;
            sunLight.transform.rotation = Quaternion.LookRotation(-activeDir, Vector3.up);

            // Цвет и интенсивность (с минимальным порогом)
            sunLight.color = _currentSwitchedState.lightColor;
            float intensity = Mathf.Max(_currentSwitchedState.lightIntensity, minLightIntensity);
            sunLight.intensity = intensity;

            // Чтобы тени не были слишком жёсткими
            sunLight.shadowStrength = 0.75f;
        }

        // Ambient (очень важно против чёрных теней)
        Color targetAmbient = ambientDay;
        if (currentStateName == "Evening") targetAmbient = ambientEvening;
        else if (currentStateName == "Night") targetAmbient = ambientNight;

        RenderSettings.ambientLight = targetAmbient * ambientIntensity;
        RenderSettings.ambientIntensity = 1f;

        // === Передаём данные в шейдер ===
        if (skyboxMaterial != null)
        {
            if (_cachedMoonTex != null && skyboxMaterial.HasProperty(MoonTexId) &&
                skyboxMaterial.GetTexture(MoonTexId) != _cachedMoonTex)
                skyboxMaterial.SetTexture(MoonTexId, _cachedMoonTex);

            if (skyboxMaterial.HasProperty(HasScriptDataId)) skyboxMaterial.SetFloat(HasScriptDataId, 1f);
            if (skyboxMaterial.HasProperty(SimTimeId)) skyboxMaterial.SetFloat(SimTimeId, simTime);
            if (skyboxMaterial.HasProperty(SunDirId)) skyboxMaterial.SetVector(SunDirId, toSun);
            if (skyboxMaterial.HasProperty(MoonDirId)) skyboxMaterial.SetVector(MoonDirId, toMoon);

            if (skyboxMaterial.HasProperty(SkyTopId)) skyboxMaterial.SetColor(SkyTopId, _currentSwitchedState.skyTop);
            if (skyboxMaterial.HasProperty(SkyHorizonId)) skyboxMaterial.SetColor(SkyHorizonId, _currentSwitchedState.skyHorizon);
            if (skyboxMaterial.HasProperty(HorizonGlowColorId)) skyboxMaterial.SetColor(HorizonGlowColorId, _currentSwitchedState.horizonGlowColor);
            if (skyboxMaterial.HasProperty(HorizonGlowStrengthId)) skyboxMaterial.SetFloat(HorizonGlowStrengthId, _currentSwitchedState.horizonGlowStrength);

            if (skyboxMaterial.HasProperty(SunColorId)) skyboxMaterial.SetColor(SunColorId, _currentSwitchedState.sunColor);
            if (skyboxMaterial.HasProperty(SunSizeId)) skyboxMaterial.SetFloat(SunSizeId, sunSize);
            if (skyboxMaterial.HasProperty(SunHorizonBoostId)) skyboxMaterial.SetFloat(SunHorizonBoostId, sunHorizonBoost);
            if (skyboxMaterial.HasProperty(SunCoronaStrengthId)) skyboxMaterial.SetFloat(SunCoronaStrengthId, _currentSwitchedState.sunCoronaStrength);
            if (skyboxMaterial.HasProperty(SunFXStrengthId)) skyboxMaterial.SetFloat(SunFXStrengthId, _currentSwitchedState.sunFXStrength);
            if (skyboxMaterial.HasProperty(RaysStrengthId)) skyboxMaterial.SetFloat(RaysStrengthId, _currentSwitchedState.raysStrength);
            if (skyboxMaterial.HasProperty(GlareStrengthId)) skyboxMaterial.SetFloat(GlareStrengthId, _currentSwitchedState.glareStrength);

            if (skyboxMaterial.HasProperty(MoonSizeId)) skyboxMaterial.SetFloat(MoonSizeId, moonSize);
            if (skyboxMaterial.HasProperty(MoonHaloStrengthId)) skyboxMaterial.SetFloat(MoonHaloStrengthId, moonHaloStrength);
            if (skyboxMaterial.HasProperty(StarsVisibilityId)) skyboxMaterial.SetFloat(StarsVisibilityId, _currentSwitchedState.starsVisibility);

            if (skyboxMaterial.HasProperty(CloudScaleId)) skyboxMaterial.SetFloat(CloudScaleId, cloudScale);
            if (skyboxMaterial.HasProperty(CloudSpeedId)) skyboxMaterial.SetVector(CloudSpeedId, new Vector4(cloudSpeed.x, cloudSpeed.y, 0, 0));
            if (skyboxMaterial.HasProperty(CloudWarpId)) skyboxMaterial.SetFloat(CloudWarpId, cloudWarp);
            if (skyboxMaterial.HasProperty(CloudCoverageId)) skyboxMaterial.SetFloat(CloudCoverageId, _currentSwitchedState.cloudCoverage);
            if (skyboxMaterial.HasProperty(CloudBrightId)) skyboxMaterial.SetColor(CloudBrightId, _currentSwitchedState.cloudBright);
            if (skyboxMaterial.HasProperty(CloudDarkId)) skyboxMaterial.SetColor(CloudDarkId, _currentSwitchedState.cloudDark);
            if (skyboxMaterial.HasProperty(CloudSunLightId)) skyboxMaterial.SetFloat(CloudSunLightId, _currentSwitchedState.cloudSunLight);
            if (skyboxMaterial.HasProperty(CloudShadowStrengthId)) skyboxMaterial.SetFloat(CloudShadowStrengthId, cloudShadowStrength);
            if (skyboxMaterial.HasProperty(CloudAmbientId)) skyboxMaterial.SetFloat(CloudAmbientId, cloudAmbient);

            if (skyboxMaterial.HasProperty(ColorShiftSpeedId)) skyboxMaterial.SetFloat(ColorShiftSpeedId, colorShiftSpeed);
            if (skyboxMaterial.HasProperty(ColorShiftAmountId)) skyboxMaterial.SetFloat(ColorShiftAmountId, colorShiftAmount);
        }
    }

    private SkyState PickTargetState(float sunY)
    {
        if (sunY > eveningLead) return dayState;
        if (sunY > -nightLead) return eveningState;
        return nightState;
    }

    private static SkyState StepTowardState(SkyState current, SkyState target, float step)
    {
        return new SkyState
        {
            name = target.name,
            skyTop = MoveColor(current.skyTop, target.skyTop, step),
            skyHorizon = MoveColor(current.skyHorizon, target.skyHorizon, step),
            horizonGlowColor = MoveColor(current.horizonGlowColor, target.horizonGlowColor, step),
            horizonGlowStrength = Mathf.MoveTowards(current.horizonGlowStrength, target.horizonGlowStrength, step),
            lightColor = MoveColor(current.lightColor, target.lightColor, step),
            lightIntensity = Mathf.MoveTowards(current.lightIntensity, target.lightIntensity, step),
            cloudBright = MoveColor(current.cloudBright, target.cloudBright, step),
            cloudDark = MoveColor(current.cloudDark, target.cloudDark, step),
            cloudCoverage = Mathf.MoveTowards(current.cloudCoverage, target.cloudCoverage, step),
            cloudSunLight = Mathf.MoveTowards(current.cloudSunLight, target.cloudSunLight, step),
            sunColor = MoveColor(current.sunColor, target.sunColor, step),
            sunCoronaStrength = Mathf.MoveTowards(current.sunCoronaStrength, target.sunCoronaStrength, step),
            sunFXStrength = Mathf.MoveTowards(current.sunFXStrength, target.sunFXStrength, step),
            glareStrength = Mathf.MoveTowards(current.glareStrength, target.glareStrength, step),
            raysStrength = Mathf.MoveTowards(current.raysStrength, target.raysStrength, step),
            starsVisibility = Mathf.MoveTowards(current.starsVisibility, target.starsVisibility, step)
        };
    }

    private static Color MoveColor(Color a, Color b, float step)
    {
        return new Color(
            Mathf.MoveTowards(a.r, b.r, step),
            Mathf.MoveTowards(a.g, b.g, step),
            Mathf.MoveTowards(a.b, b.b, step),
            Mathf.MoveTowards(a.a, b.a, step)
        );
    }

    private static Texture2D GenerateMoonTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "ProceduralMoonTex";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector4[] craters =
        {
            new Vector4(0.38f, 0.62f, 0.14f, 0.85f),
            new Vector4(0.62f, 0.54f, 0.11f, 0.75f),
            new Vector4(0.48f, 0.36f, 0.16f, 0.90f),
            new Vector4(0.28f, 0.42f, 0.08f, 0.65f),
            new Vector4(0.68f, 0.32f, 0.09f, 0.70f),
            new Vector4(0.55f, 0.72f, 0.07f, 0.60f),
            new Vector4(0.33f, 0.73f, 0.06f, 0.55f),
            new Vector4(0.73f, 0.65f, 0.06f, 0.55f),
            new Vector4(0.43f, 0.50f, 0.05f, 0.50f),
            new Vector4(0.59f, 0.43f, 0.05f, 0.50f)
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = (y / (float)(size - 1)) * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x / (float)(size - 1)) * 2f - 1f;
                float r2 = nx * nx + ny * ny;
                float z = Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Clamp01(r2) * 0.85f));

                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);

                float maria = Mathf.PerlinNoise(u * 4.5f + 2.1f, v * 4.5f + 7.4f);
                float detail = Mathf.PerlinNoise(u * 14f + 11.2f, v * 14f + 3.8f);

                float craterDelta = 0f;
                for (int c = 0; c < craters.Length; c++)
                {
                    Vector4 cr = craters[c];
                    float dx = (u - cr.x) / cr.z;
                    float dy = (v - cr.y) / cr.z;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < 1.35f)
                    {
                        float bowl = 1f - Mathf.SmoothStep(0f, 0.88f, dist);
                        float rim = Mathf.Exp(-Mathf.Pow((dist - 0.96f) * 6f, 2f));
                        float dirShade = (dx * 0.6f + dy * 0.4f) * 0.25f;
                        craterDelta += (-bowl * 0.55f + rim * 0.45f + dirShade * bowl) * cr.w;
                    }
                }

                float h = Mathf.Clamp01(0.72f + (maria - 0.5f) * 0.35f + (detail - 0.5f) * 0.15f + craterDelta * 0.32f);
                float shade = h * Mathf.Lerp(0.78f, 1.12f, z);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(shade * 255f), 0, 255);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}