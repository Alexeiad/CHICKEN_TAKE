Shader "Custom/Sky"
{
    Properties
    {
        [Header(Day Evening Night Cycle)]
        _TransitionWidth ("Transition Width (Bigger = Slower Blend)", Range(0.3, 3.0)) = 1.0
        _DuskTop ("Dusk Sky Top Color", Color) = (0.30, 0.20, 0.46, 1)
        _DuskHorizon ("Dusk Sky Horizon Color", Color) = (1.00, 0.52, 0.30, 1)
        _NightTop ("Night Sky Top Color", Color) = (0.006, 0.012, 0.045, 1)
        _NightHorizon ("Night Sky Horizon Color", Color) = (0.035, 0.055, 0.13, 1)
        _DayGlowMul ("Horizon Glow Mul (Day)", Range(0.0, 2.0)) = 0.35
        _DuskGlowMul ("Horizon Glow Mul (Dusk)", Range(0.0, 3.0)) = 1.6
        _NightGlowMul ("Horizon Glow Mul (Night)", Range(0.0, 2.0)) = 0.06
        _NightCloudTint ("Night Clouds Tint", Color) = (0.16, 0.20, 0.34, 1)
        _DuskCloudTint ("Dusk Clouds Tint", Color) = (1.15, 0.80, 0.72, 1)

        [Header(Blended Sky Colors Day)]
        _SkyTop ("Sky Top Color", Color) = (0.14, 0.46, 0.92, 1)
        _SkyHorizon ("Sky Horizon Color", Color) = (0.68, 0.86, 0.99, 1)
        _HorizonGlowColor ("Horizon Glow Color", Color) = (1.00, 0.56, 0.22, 1)
        _HorizonGlowStrength ("Horizon Glow Strength", Range(0.0, 2.5)) = 0.65
        _HorizonBandSharpness ("Horizon Band Sharpness", Range(1.0, 24.0)) = 7.5
        _Ground ("Ground Color", Color) = (0.15, 0.16, 0.19, 1)

        [Header(Sun And Camera FX)]
        _SunColor ("Sun Color", Color) = (1.00, 0.96, 0.85, 1)
        _SunSize ("Sun Size", Range(0.002, 0.06)) = 0.013
        _SunHorizonBoost ("Sun Horizon Boost", Range(0.0, 2.0)) = 0.75
        _SunSoftness ("Sun Edge Softness", Range(0.0002, 0.02)) = 0.0016
        _SunCoronaStrength ("Sun Corona Strength", Range(0.0, 3.5)) = 1.35
        _SunFXStrength ("Sun Rays And Glare", Range(0.0, 3.0)) = 1.25
        _RaysStrength ("God Rays Strength", Range(0.0, 3.0)) = 1.15
        _GlareStrength ("Camera Glare Strength", Range(0.0, 3.0)) = 1.05
        _StreakStrength ("Anamorphic Streak", Range(0.0, 2.5)) = 0.65

        [Header(Moon Generated Once And Stars)]
        _MoonTex ("Moon Texture (Generated Once)", 2D) = "white" {}
        _MoonColor ("Moon Color", Color) = (0.88, 0.92, 1.00, 1)
        _MoonSize ("Moon Size", Range(0.006, 0.09)) = 0.026
        _MoonSoftness ("Moon Edge Softness", Range(0.0003, 0.02)) = 0.0020
        _MoonHaloStrength ("Moon Halo Strength", Range(0.0, 2.0)) = 0.55
        _StarsVisibility ("Stars Visibility Extra", Range(0.0, 1.0)) = 0.0
        _StarsBrightness ("Stars Brightness", Range(0.0, 3.0)) = 1.65

        [Header(Clouds Cumulus Style)]
        _CloudScale ("Clouds Scale (Smaller = Bigger)", Range(0.25, 3.5)) = 0.58
        _CloudSpeed ("Clouds Wind (X, Z)", Vector) = (0.045, 0.022, 0, 0)
        _CloudWarp ("Clouds Living Warp", Range(0.0, 2.0)) = 1.35
        _CloudCoverage ("Clouds Coverage", Range(0.0, 1.0)) = 0.68
        _CloudSoftness ("Clouds Softness", Range(0.03, 0.6)) = 0.19
        _CloudBright ("Clouds Bright Color", Color) = (1.00, 0.99, 0.96, 1)
        _CloudDark ("Clouds Shadow Color", Color) = (0.32, 0.36, 0.48, 1)
        _CloudSunLight ("Clouds Sun Lighting", Range(0.0, 1.0)) = 1.0
        _CloudShadowStrength ("Cloud Shadow Strength", Range(0.0, 1.5)) = 0.70
        _CloudAmbient ("Cloud Ambient Light", Range(0.0, 1.0)) = 0.28

        [Header(Time And Color Motion)]
        _SimTime ("Simulation Time", Float) = 0.0
        _ColorShiftSpeed ("Color Shift Speed", Range(0.0, 2.0)) = 0.22
        _ColorShiftAmount ("Color Shift Amount", Range(0.0, 0.25)) = 0.06
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _TransitionWidth;
            fixed4 _DuskTop;
            fixed4 _DuskHorizon;
            fixed4 _NightTop;
            fixed4 _NightHorizon;
            float _DayGlowMul;
            float _DuskGlowMul;
            float _NightGlowMul;
            fixed4 _NightCloudTint;
            fixed4 _DuskCloudTint;

            fixed4 _SkyTop;
            fixed4 _SkyHorizon;
            fixed4 _HorizonGlowColor;
            float _HorizonGlowStrength;
            float _HorizonBandSharpness;
            fixed4 _Ground;

            fixed4 _SunColor;
            float _SunSize;
            float _SunHorizonBoost;
            float _SunSoftness;
            float _SunCoronaStrength;
            float _SunFXStrength;
            float _RaysStrength;
            float _GlareStrength;
            float _StreakStrength;

            sampler2D _MoonTex;
            fixed4 _MoonColor;
            float _MoonSize;
            float _MoonSoftness;
            float _MoonHaloStrength;
            float _StarsVisibility;
            float _StarsBrightness;

            float _CloudScale;
            float4 _CloudSpeed;
            float _CloudWarp;
            float _CloudCoverage;
            float _CloudSoftness;
            fixed4 _CloudBright;
            fixed4 _CloudDark;
            float _CloudSunLight;
            float _CloudShadowStrength;
            float _CloudAmbient;

            float _SimTime;
            float _ColorShiftSpeed;
            float _ColorShiftAmount;

            float3 _SunDir;
            float3 _MoonDir;
            float _HasScriptData;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float3 hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(hash21(i), hash21(i + float2(1.0, 0.0)), u.x),
                    lerp(hash21(i + float2(0.0, 1.0)), hash21(i + float2(1.0, 1.0)), u.x),
                    u.y
                );
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(0.80, -0.60, 0.60, 0.80);

                for (int i = 0; i < 6; i++)
                {
                    v += a * vnoise(p);
                    p = mul(rot, p) * 2.05;
                    a *= 0.5;
                }
                return v;
            }

            // Более "кучевой" шум
            float cumulusNoise(float2 p)
            {
                float n = fbm(p);
                n = fbm(p + n * 0.7);
                n = pow(saturate(n * 1.15), 1.35);
                return n;
            }

            fixed3 PulseColor(fixed3 c, float t, float phase)
            {
                float3 w = float3(
                    sin(t * _ColorShiftSpeed + phase),
                    sin(t * _ColorShiftSpeed * 0.85 + phase + 2.094),
                    sin(t * _ColorShiftSpeed * 1.15 + phase + 4.188)
                );
                return max(c * (1.0 + w * _ColorShiftAmount), fixed3(0.0, 0.0, 0.0));
            }

            void BuildBasis(float3 dir, out float3 rightAx, out float3 upAx)
            {
                float3 upRef = abs(dir.y) < 0.95 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                rightAx = normalize(cross(upRef, dir));
                upAx = cross(dir, rightAx);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 fallbackSun = normalize(_WorldSpaceLightPos0.xyz);
                float3 sunDir = _HasScriptData > 0.5 ? normalize(_SunDir) : fallbackSun;
                float3 moonDir = _HasScriptData > 0.5 ? normalize(_MoonDir) : normalize(-fallbackSun);
                float t = _HasScriptData > 0.5 ? _SimTime : _Time.y;

                float up = saturate(d.y);
                float phase = d.x * 1.4 + d.z * 1.1 + d.y * 1.8;

                // 0. Фаза суток по высоте солнца: день -> вечер/рассвет -> ночь (плавно)
                float k = max(_TransitionWidth, 0.1);
                float sunH = sunDir.y;
                float nightW = 1.0 - smoothstep(-0.28 * k, -0.04 * k, sunH);
                float dayW = smoothstep(0.02 * k, 0.32 * k, sunH);
                float duskW = saturate(1.0 - nightW - dayW);

                // 1. Небо
                fixed3 topCol =
                    PulseColor(_SkyTop.rgb, t, 0.2 + phase * 0.2) * dayW +
                    PulseColor(_DuskTop.rgb, t, 0.6 + phase * 0.2) * duskW +
                    PulseColor(_NightTop.rgb, t, 0.9 + phase * 0.2) * nightW;

                fixed3 horCol =
                    PulseColor(_SkyHorizon.rgb, t, 1.4 + phase * 0.2) * dayW +
                    PulseColor(_DuskHorizon.rgb, t, 1.8 + phase * 0.2) * duskW +
                    PulseColor(_NightHorizon.rgb, t, 2.1 + phase * 0.2) * nightW;

                fixed3 sky = lerp(horCol, topCol, pow(up, 0.55));

                float sunAlign = max(dot(d, sunDir), 0.0);
                float sunHorizonFactor = 1.0 - smoothstep(0.0, 0.55, abs(sunDir.y));
                float horizonLine = exp(-abs(d.y) * _HorizonBandSharpness);
                float sunDirBoost = 0.35 + 0.65 * pow(sunAlign, 2.0);
                float glowMul = _DayGlowMul * dayW + _DuskGlowMul * duskW + _NightGlowMul * nightW;
                fixed3 hGlowCol = PulseColor(_HorizonGlowColor.rgb, t, 2.2);
                sky += hGlowCol * horizonLine * sunDirBoost * _HorizonGlowStrength * glowMul * (0.35 + 0.65 * sunHorizonFactor);

                if (d.y < 0.0)
                {
                    float groundLight = lerp(0.22, 1.0, saturate(dayW + duskW * 0.55));
                    sky = lerp(sky, _Ground.rgb * groundLight, saturate(-d.y * 4.5));
                }

                // 2. Звёзды (появляются сами по мере наступления ночи)
                fixed3 stars = fixed3(0, 0, 0);
                float starsAuto = saturate(nightW + duskW * 0.12);
                float starVis = saturate(_StarsVisibility + starsAuto) * smoothstep(0.02, 0.25, d.y);
                if (starVis > 0.001)
                {
                    float3 cell = floor(d * 135.0);
                    float3 rnd = hash33(cell);
                    if (rnd.x > 0.985)
                    {
                        float3 center = normalize(cell + 0.5);
                        float dist = length(d - center) * 135.0;
                        float dotStar = 1.0 - smoothstep(0.0, 0.44, dist);
                        float blink = 0.7 + 0.3 * sin(t * 2.2 + rnd.y * 45.0);
                        stars = PulseColor(fixed3(0.85, 0.91, 1.0), t, rnd.y * 6.0) * dotStar * blink * _StarsBrightness * starVis * 2.1;
                    }
                }

                // 3. Кучевые облака
                float cloudDens = 0.0;
                fixed3 cloudCol = sky;
                float cloudOcclusion = 0.0;

                if (d.y > 0.001)
                {
                    float edgeFade = smoothstep(0.001, 0.32, d.y);

                    const int STEPS = 6;
                    float maxHeight = 0.62;
                    float stepSize = maxHeight / STEPS;

                    float totalDens = 0.0;
                    float transmittance = 1.0;
                    fixed3 accumLight = fixed3(0, 0, 0);

                    float coverage = saturate(_CloudCoverage);
                    float baseThreshold = lerp(0.90, 0.29, coverage);
                    float softness = _CloudSoftness * lerp(1.2, 0.65, coverage);

                    float3 lightDir = sunDir;
                    float sunAbove = smoothstep(-0.06, 0.14, sunDir.y);
                    float sunPower = saturate(_CloudSunLight) * sunAbove;

                    float shadowStr = saturate(_CloudShadowStrength);
                    float ambient = saturate(_CloudAmbient);

                    for (int s = 0; s < STEPS; s++)
                    {
                        float h = (s + 0.35) * stepSize;
                        float3 pos = d * (0.12 + h * 1.9);

                        float2 uv = pos.xz / (pos.y + 0.20) * _CloudScale;
                        uv += t * _CloudSpeed.xy * (0.75 + h * 0.4);

                        float morph = t * 0.038 + h * 2.3;
                        float2 warp = float2(
                            fbm(uv * 0.8 + float2(morph, -morph * 0.7)),
                            fbm(uv * 0.8 + float2(4.4, 1.8) - float2(morph * 0.5, morph * 0.85))
                        );

                        float2 sampleUV = uv + _CloudWarp * 1.55 * warp * (1.0 - h * 0.45);
                        float n = cumulusNoise(sampleUV);

                        float heightMask = smoothstep(0.0, 0.10, h) * smoothstep(maxHeight, maxHeight * 0.48, h);
                        heightMask = pow(heightMask, 0.68);

                        float dens = smoothstep(baseThreshold, baseThreshold + softness, n) * heightMask;
                        dens *= 1.25;

                        float shadow = exp(-totalDens * (1.4 + shadowStr * 1.8));
                        shadow = lerp(1.0, shadow, shadowStr);
                        shadow = max(shadow, ambient * 0.55);

                        float sunAlignLocal = max(dot(normalize(pos), lightDir), 0.0);
                        float phaseFunc = 0.35 + 0.65 * pow(sunAlignLocal, 1.25);

                        fixed3 bright = PulseColor(_CloudBright.rgb, t, 0.45 + n * 1.5);
                        fixed3 dark   = PulseColor(_CloudDark.rgb,   t, 2.0 + n * 1.3);

                        fixed3 ambientCol = lerp(dark * 1.05, bright * 0.9, 0.25) * ambient;
                        fixed3 sunLit = lerp(dark, bright, phaseFunc) * sunPower;

                        float rim = saturate(pow(saturate(n - baseThreshold + 0.18), 1.4)) * sunPower;
                        fixed3 rimCol = PulseColor(lerp(_SunColor.rgb, _HorizonGlowColor.rgb, 0.4), t, 1.25) * rim * 1.35;

                        fixed3 lightCol = ambientCol + sunLit * shadow + rimCol;

                        float sampleWeight = dens * transmittance * stepSize * 4.4;
                        accumLight += lightCol * sampleWeight;
                        totalDens += dens * stepSize * 3.8;
                        transmittance *= exp(-dens * stepSize * 4.1);

                        if (transmittance < 0.02) break;
                    }

                    cloudDens = saturate(1.0 - transmittance) * edgeFade;
                    cloudCol = accumLight / max(totalDens * 0.28, 0.001);

                    // Окраска облаков по времени суток: вечером тёплые, ночью тёмно-синие
                    float3 cloudTint = float3(1.0, 1.0, 1.0);
                    cloudTint = lerp(cloudTint, _DuskCloudTint.rgb, duskW);
                    cloudTint = lerp(cloudTint, _NightCloudTint.rgb, nightW);
                    cloudCol *= cloudTint;

                    cloudCol = lerp(sky * 0.5, cloudCol, 0.96);

                    cloudOcclusion = saturate(cloudDens * 1.4);
                }

                // 4. Солнце
                float sunHorizonMask = smoothstep(-0.02, 0.04, sunDir.y) * smoothstep(-0.02, 0.04, d.y);

                float3 sunRight, sunUp;
                BuildBasis(sunDir, sunRight, sunUp);
                float2 sunRel = float2(dot(d - sunDir, sunRight), dot(d - sunDir, sunUp));
                float sunAngle = atan2(sunRel.y, sunRel.x);

                float horizBoost = 1.0 - smoothstep(0.0, 0.45, abs(sunDir.y));
                float rSun = _SunSize * (1.0 + _SunHorizonBoost * horizBoost);
                float sunDist = 1.0 - sunAlign;

                float sunDisk = (1.0 - smoothstep(rSun - _SunSoftness, rSun + _SunSoftness, sunDist)) * sunHorizonMask;
                float corona = exp(-sunDist / max(rSun * 4.2, 0.005)) * _SunCoronaStrength * sunHorizonMask;

                float fxMask = saturate(_SunFXStrength) * smoothstep(-0.04, 0.14, sunDir.y);

                float raysWave = pow(0.5 + 0.5 * sin(sunAngle * 18.0 + t * 0.22), 2.6) * 0.6
                               + pow(0.5 + 0.5 * sin(sunAngle * 11.0 - t * 0.16 + 1.3), 2.3) * 0.4;
                float rays = raysWave * pow(sunAlign, 3.2) * (0.35 + cloudOcclusion * 1.1) * _RaysStrength * fxMask;

                float glare = (pow(sunAlign, 7.0) * _GlareStrength + pow(sunAlign, 2.2) * 0.22 * _GlareStrength) * fxMask;
                float streak = exp(-abs(sunRel.y) * 38.0) * exp(-abs(sunRel.x) * 1.8) * pow(sunAlign, 2.6) * _StreakStrength * fxMask;

                float halo1 = exp(-abs(length(sunRel) - 0.19) * 36.0) * 0.7;
                float halo2 = exp(-abs(length(sunRel) - 0.36) * 28.0) * 0.45;
                float rings = (halo1 + halo2) * pow(sunAlign, 2.0) * _GlareStrength * 0.7 * fxMask;

                // Вечером солнце краснеет
                float3 sunBase = lerp(_SunColor.rgb, _HorizonGlowColor.rgb * 1.15, saturate(duskW * 0.75));
                fixed3 sunCoreCol = PulseColor(sunBase, t, 1.1) * 2.8;
                fixed3 fxCol = PulseColor(lerp(sunBase, _HorizonGlowColor.rgb, 0.4), t, 1.7);

                // 5. Луна
                float moonHorizonMask = smoothstep(-0.02, 0.04, moonDir.y) * smoothstep(-0.02, 0.04, d.y);

                float3 moonRight, moonUp;
                BuildBasis(moonDir, moonRight, moonUp);
                float2 moonRel = float2(dot(d - moonDir, moonRight), dot(d - moonDir, moonUp));

                float moonAlign = max(dot(d, moonDir), 0.0);
                float moonDist = 1.0 - moonAlign;
                float moonDiskMask = (1.0 - smoothstep(_MoonSize - _MoonSoftness, _MoonSize + _MoonSoftness, moonDist)) * moonHorizonMask;

                // Днём луна почти не видна, к ночи проявляется
                float moonVis = 1.0 - dayW * 0.8;
                float moonHalo = exp(-moonDist / max(_MoonSize * 3.1, 0.005)) * _MoonHaloStrength * moonHorizonMask * (nightW + duskW * 0.35);

                float2 moonUV = saturate(moonRel / max(_MoonSize * 2.5, 0.004) + 0.5);
                fixed3 moonTexSample = tex2D(_MoonTex, moonUV).rgb;

                float moonLimb = pow(saturate(1.0 - length(moonRel) / max(_MoonSize, 0.001)), 0.45);
                fixed3 moonFinalCol = PulseColor(_MoonColor.rgb, t, 0.85) * moonTexSample * 1.9 * moonLimb;

                // Сборка
                fixed3 col = sky;
                col += PulseColor(_MoonColor.rgb, t, 1.15) * moonHalo * 1.1;
                col += stars * (1.0 - saturate(cloudDens * 1.35));
                col = lerp(col, cloudCol, saturate(cloudDens));

                col += fxCol * (corona * 1.15 + rays + glare + streak + rings);

                float occ = 1.0 - cloudDens * 0.82;
                col = lerp(col, sunCoreCol, sunDisk * occ);
                col = lerp(col, moonFinalCol, moonDiskMask * moonVis * (1.0 - cloudDens * 0.7));

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}