Shader "Custom/Sky"
{
    Properties
    {
        [Header(Blended Sky Colors)]
        _SkyTop ("Sky Top Color", Color) = (0.14, 0.46, 0.92, 1)
        _SkyHorizon ("Sky Horizon Color", Color) = (0.68, 0.86, 0.99, 1)
        _HorizonGlowColor ("Horizon Glow Color", Color) = (1.00, 0.56, 0.22, 1)
        _HorizonGlowStrength ("Horizon Glow Strength", Range(0.0, 2.5)) = 0.65
        _HorizonBandSharpness ("Horizon Band Sharpness", Range(1.0, 24.0)) = 7.5
        _Ground ("Ground Color", Color) = (0.15, 0.16, 0.19, 1)

        [Header(Sun And Camera FX)]
        _SunColor ("Sun Color", Color) = (1.00, 0.96, 0.85, 1)
        _SunSize ("Sun Size", Range(0.002, 0.06)) = 0.011
        _SunHorizonBoost ("Sun Horizon Boost", Range(0.0, 2.0)) = 0.65
        _SunSoftness ("Sun Edge Softness", Range(0.0002, 0.02)) = 0.0014
        _SunCoronaStrength ("Sun Corona Strength", Range(0.0, 2.5)) = 0.85
        _SunFXStrength ("Sun Rays And Glare", Range(0.0, 2.5)) = 0.85
        _RaysStrength ("God Rays Strength", Range(0.0, 2.5)) = 0.65
        _GlareStrength ("Camera Glare Strength", Range(0.0, 2.5)) = 0.75
        _StreakStrength ("Anamorphic Streak", Range(0.0, 2.0)) = 0.45

        [Header(Moon Generated Once And Stars)]
        _MoonTex ("Moon Texture (Generated Once)", 2D) = "white" {}
        _MoonColor ("Moon Color", Color) = (0.90, 0.95, 1.00, 1)
        _MoonSize ("Moon Size", Range(0.006, 0.09)) = 0.024
        _MoonSoftness ("Moon Edge Softness", Range(0.0003, 0.02)) = 0.0018
        _MoonHaloStrength ("Moon Halo Strength", Range(0.0, 1.5)) = 0.35
        _StarsVisibility ("Stars Visibility", Range(0.0, 1.0)) = 0.0
        _StarsBrightness ("Stars Brightness", Range(0.0, 3.0)) = 1.65

        [Header(Clouds)]
        _CloudScale ("Clouds Scale (Smaller = Bigger)", Range(0.35, 4.0)) = 0.72
        _CloudSpeed ("Clouds Wind (X, Z)", Vector) = (0.028, 0.012, 0, 0)
        _CloudWarp ("Clouds Living Warp", Range(0.0, 1.5)) = 1.15
        _CloudCoverage ("Clouds Coverage", Range(0.0, 1.0)) = 0.72
        _CloudSoftness ("Clouds Softness", Range(0.03, 0.6)) = 0.21
        _CloudBright ("Clouds Bright Color", Color) = (1.00, 0.99, 0.97, 1)
        _CloudDark ("Clouds Shadow Color", Color) = (0.36, 0.42, 0.54, 1)
        _CloudSunLight ("Clouds Sun Lighting", Range(0.0, 1.0)) = 1.0

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

                for (int i = 0; i < 5; i++)
                {
                    v += a * vnoise(p);
                    p = mul(rot, p) * 2.03;
                    a *= 0.5;
                }
                return v;
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

                // 1. Небо + пояс горизонта
                fixed3 topCol = PulseColor(_SkyTop.rgb, t, 0.2 + phase * 0.2);
                fixed3 horCol = PulseColor(_SkyHorizon.rgb, t, 1.4 + phase * 0.2);
                fixed3 sky = lerp(horCol, topCol, pow(up, 0.55));

                float sunAlign = max(dot(d, sunDir), 0.0);
                float sunHorizonFactor = 1.0 - smoothstep(0.0, 0.55, abs(sunDir.y));
                float horizonLine = exp(-abs(d.y) * _HorizonBandSharpness);
                float sunDirBoost = 0.35 + 0.65 * pow(sunAlign, 2.0);
                fixed3 hGlowCol = PulseColor(_HorizonGlowColor.rgb, t, 2.2);
                sky += hGlowCol * horizonLine * sunDirBoost * _HorizonGlowStrength * (0.35 + 0.65 * sunHorizonFactor);

                if (d.y < 0.0)
                    sky = lerp(sky, _Ground.rgb, saturate(-d.y * 4.5));

                // 2. Звёзды
                fixed3 stars = fixed3(0, 0, 0);
                float starVis = saturate(_StarsVisibility) * smoothstep(0.02, 0.25, d.y);
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

                // 3. Облака — объёмные (фейковый raymarch)
                float cloudDens = 0.0;
                fixed3 cloudCol = sky;

                if (d.y > 0.001)
                {
                    float edgeFade = smoothstep(0.001, 0.28, d.y);

                    const int STEPS = 5;
                    float maxHeight = 0.55;
                    float stepSize = maxHeight / STEPS;

                    float totalDens = 0.0;
                    float transmittance = 1.0;
                    fixed3 accumLight = fixed3(0, 0, 0);

                    float coverage = saturate(_CloudCoverage);
                    float baseThreshold = lerp(0.88, 0.32, coverage);
                    float softness = _CloudSoftness * lerp(1.15, 0.7, coverage);

                    float3 lightDir = sunDir;
                    float sunAbove = smoothstep(-0.05, 0.15, sunDir.y);
                    float sunPower = saturate(_CloudSunLight) * sunAbove;

                    for (int s = 0; s < STEPS; s++)
                    {
                        float h = (s + 0.4) * stepSize;
                        float3 pos = d * (0.15 + h * 1.8);

                        float2 uv = pos.xz / (pos.y + 0.22) * _CloudScale;
                        uv += t * _CloudSpeed.xy * (0.7 + h * 0.4);

                        float morph = t * 0.04 + h * 2.1;
                        float2 warp = float2(
                            fbm(uv * 0.85 + float2(morph, -morph * 0.65)),
                            fbm(uv * 0.85 + float2(4.2, 1.9) - float2(morph * 0.55, morph * 0.8))
                        );

                        float2 sampleUV = uv + _CloudWarp * 1.4 * warp * (1.0 - h * 0.5);
                        float n = fbm(sampleUV);
                        n = fbm(sampleUV + n * 0.6);

                        float heightMask = smoothstep(0.0, 0.12, h) * smoothstep(maxHeight, maxHeight * 0.55, h);
                        heightMask = pow(heightMask, 0.75);

                        float dens = smoothstep(baseThreshold, baseThreshold + softness, n) * heightMask;
                        dens *= 1.15;

                        float shadow = exp(-totalDens * 2.8);

                        float sunAlignLocal = max(dot(normalize(pos), lightDir), 0.0);
                        float phaseFunc = 0.55 + 0.45 * pow(sunAlignLocal, 1.6);

                        fixed3 bright = PulseColor(_CloudBright.rgb, t, 0.5 + n * 1.6);
                        fixed3 dark   = PulseColor(_CloudDark.rgb,   t, 2.1 + n * 1.4);

                        float rim = saturate(pow(saturate(n - baseThreshold + 0.22), 1.7)) * sunPower;
                        fixed3 lightCol = lerp(dark, bright, phaseFunc);
                        lightCol += PulseColor(lerp(_SunColor.rgb, _HorizonGlowColor.rgb, 0.5), t, 1.3) * rim * 1.1;
                        lightCol += bright * 0.12 * sunPower;

                        float sampleWeight = dens * transmittance * stepSize * 4.2;
                        accumLight += lightCol * sampleWeight * shadow;
                        totalDens += dens * stepSize * 3.5;
                        transmittance *= exp(-dens * stepSize * 3.8);

                        if (transmittance < 0.02) break;
                    }

                    cloudDens = saturate(1.0 - transmittance) * edgeFade;
                    cloudCol = accumLight / max(totalDens * 0.35, 0.001);
                    cloudCol = lerp(sky * 0.7, cloudCol, 0.92);
                }

                // Скрытие за горизонтом
                float sunHorizonMask = smoothstep(-0.015, 0.03, sunDir.y) * smoothstep(-0.015, 0.03, d.y);
                float moonHorizonMask = smoothstep(-0.015, 0.03, moonDir.y) * smoothstep(-0.015, 0.03, d.y);

                // 4. Солнце
                float3 sunRight, sunUp;
                BuildBasis(sunDir, sunRight, sunUp);
                float2 sunRel = float2(dot(d - sunDir, sunRight), dot(d - sunDir, sunUp));
                float sunAngle = atan2(sunRel.y, sunRel.x);

                float horizBoost = 1.0 - smoothstep(0.0, 0.45, abs(sunDir.y));
                float rSun = _SunSize * (1.0 + _SunHorizonBoost * horizBoost);
                float sunDist = 1.0 - sunAlign;

                float sunDisk = (1.0 - smoothstep(rSun - _SunSoftness, rSun + _SunSoftness, sunDist)) * sunHorizonMask;
                float corona = exp(-sunDist / max(rSun * 3.8, 0.004)) * _SunCoronaStrength * sunHorizonMask;

                float fxMask = saturate(_SunFXStrength) * smoothstep(-0.03, 0.12, sunDir.y);
                float raysWave = pow(0.5 + 0.5 * sin(sunAngle * 22.0 + t * 0.25), 2.4) * 0.65
                               + pow(0.5 + 0.5 * sin(sunAngle * 13.0 - t * 0.18 + 1.4), 2.2) * 0.35;
                float rays = raysWave * pow(sunAlign, 3.6) * (1.0 - cloudDens * 0.65) * _RaysStrength * fxMask;

                float glare = (pow(sunAlign, 6.5) * _GlareStrength + pow(sunAlign, 2.0) * 0.16 * _GlareStrength) * fxMask;
                float streak = exp(-abs(sunRel.y) * 42.0) * exp(-abs(sunRel.x) * 2.0) * pow(sunAlign, 2.8) * _StreakStrength * fxMask;
                float rings = (exp(-abs(length(sunRel) - 0.17) * 42.0) * 0.55 + exp(-abs(length(sunRel) - 0.34) * 32.0) * 0.35) * pow(sunAlign, 2.2) * _GlareStrength * 0.5 * fxMask;

                fixed3 sunCoreCol = PulseColor(_SunColor.rgb, t, 1.1);
                fixed3 fxCol = PulseColor(lerp(_SunColor.rgb, _HorizonGlowColor.rgb, 0.45), t, 1.8);

                // 5. Луна
                float3 moonRight, moonUp;
                BuildBasis(moonDir, moonRight, moonUp);
                float2 moonRel = float2(dot(d - moonDir, moonRight), dot(d - moonDir, moonUp));

                float moonAlign = max(dot(d, moonDir), 0.0);
                float moonDist = 1.0 - moonAlign;
                float moonDiskMask = (1.0 - smoothstep(_MoonSize - _MoonSoftness, _MoonSize + _MoonSoftness, moonDist)) * moonHorizonMask;
                float moonHalo = exp(-moonDist / max(_MoonSize * 2.8, 0.004)) * _MoonHaloStrength * moonHorizonMask;

                float2 moonUV = saturate(moonRel / max(_MoonSize * 2.6, 0.004) + 0.5);
                fixed3 moonTexSample = tex2D(_MoonTex, moonUV).rgb;
                fixed3 moonFinalCol = PulseColor(_MoonColor.rgb, t, 0.9) * moonTexSample * 1.75;

                // Сборка
                fixed3 col = sky;
                col += PulseColor(_MoonColor.rgb, t, 1.2) * moonHalo;
                col += stars * (1.0 - saturate(cloudDens * 1.2));
                col = lerp(col, cloudCol, saturate(cloudDens));
                col += fxCol * (corona + rays + glare + streak + rings);

                float occ = 1.0 - cloudDens * 0.75;
                col = lerp(col, sunCoreCol * 2.35, sunDisk * occ);
                col = lerp(col, moonFinalCol, moonDiskMask * occ);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}