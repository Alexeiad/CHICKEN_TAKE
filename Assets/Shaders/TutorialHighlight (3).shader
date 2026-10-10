Shader "Custom/TutorialHighlight"
{
    // Built-in Render Pipeline.
    // v3: настоящий прозрачный оверлей (по умолчанию только свечение), маска, 3D-волны без швов,
    //     + математические режимы: Spiral, Fractal (Julia), Plasma, Cells (Voronoi).
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseAlpha ("Base Opacity (0 = только свечение, оверлей)", Range(0,1)) = 0
        _Alpha ("Global Alpha", Range(0,1)) = 1

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10  // OneMinusSrcAlpha (Additive: One)
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Mask)]
        _MaskTex ("Mask (светлое = свечение, тёмное = нет)", 2D) = "white" {}
        _MaskStrength ("Mask Strength", Range(0,1)) = 1
        _MaskPower ("Mask Contrast", Range(0.2,4)) = 1
        [Toggle] _MaskInvert ("Invert Mask", Float) = 0

        [Header(Animation)]
        [KeywordEnum(Pulse, Scanner, Rainbow, Glitch, Sparkle, Spiral, Fractal, Plasma, Cells)] _Mode ("Animation Mode", Float) = 0
        [Toggle(_USE_WORLD_SPACE)] _WorldSpace ("Waves in World Space (иначе локально по ящику)", Float) = 0
        // xyz = направление волны, w = частота (волн на 1 юнит)
        _WaveDir ("Wave Dir (xyz) / Frequency (w)", Vector) = (1, 1, 0, 1)
        _Speed ("Speed", Range(0.1, 5)) = 1

        [Header(Math Effects)]
        _Arms ("Spiral: Arms", Range(1, 10)) = 3
        _FractalIter ("Fractal: Iterations", Range(8, 64)) = 32
        _FractalZoom ("Fractal: Zoom", Range(0.5, 6)) = 2.5
        _CellScale ("Cells: Scale", Range(1, 10)) = 4

        [Header(Pixel Blink)]
        [Toggle(_PIXEL_BLINK)] _PixelBlink ("Pixel Blink Mode (случайно мигающие пиксели)", Float) = 0
        _PixelMix ("Pixel Blink Mix (1 = только пиксели, меньше = смесь с выбранным режимом)", Range(0,1)) = 1
        _PixelScale ("Pixel Density (пикселей на юнит)", Range(4, 80)) = 24
        _PixelDensity ("Lit Pixels Amount", Range(0.01, 0.6)) = 0.12
        _BlinkRate ("Blink Rate", Range(0.2, 6)) = 1.2
        _PixelNear ("Fade: Invisible Distance (вблизи не видно)", Range(0, 30)) = 2
        _PixelFar ("Fade: Fully Visible Distance (вдали видно)", Range(0.1, 80)) = 10

        [Header(Glow)]
        [HDR] _GlowColor ("Glow Color A", Color) = (0.2, 0.9, 1.0, 1)
        [HDR] _GlowColor2 ("Glow Color B", Color) = (1.0, 0.3, 0.9, 1)
        _Intensity ("Master Intensity (0 = off)", Range(0,1)) = 1

        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.5
        _SmoothNormals ("Rim: Smooth Normals (убирает швы на гранях)", Range(0,1)) = 1

        // xyz = половинные размеры меша (у стандартного куба Unity 0.5)
        _BoxSize ("Box Half Size (xyz)", Vector) = (0.5, 0.5, 0.5, 0)
        _EdgeWidth ("Edge Width", Range(0.01, 0.4)) = 0.08
        _EdgeStrength ("Edge Strength", Range(0, 4)) = 1.5

        _ScanLines ("Scan Lines Density", Range(10, 200)) = 80
        _NoiseScale ("Sparkle / Glitch Scale", Range(2, 40)) = 14
        _Breathe ("Breathing (масштаб)", Range(0, 0.1)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _MODE_PULSE _MODE_SCANNER _MODE_RAINBOW _MODE_GLITCH _MODE_SPARKLE _MODE_SPIRAL _MODE_FRACTAL _MODE_PLASMA _MODE_CELLS
            #pragma shader_feature_local _USE_WORLD_SPACE
            #pragma shader_feature_local _PIXEL_BLINK

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float4 _MaskTex_ST;

            fixed4 _BaseColor;
            float _BaseAlpha, _Alpha;

            float _MaskStrength, _MaskPower, _MaskInvert;

            float4 _WaveDir;
            float _Speed;
            float _Arms, _FractalIter, _FractalZoom, _CellScale;
            float _PixelMix, _PixelScale, _PixelDensity, _BlinkRate, _PixelNear, _PixelFar;

            float4 _GlowColor, _GlowColor2;
            float _Intensity;
            float _RimPower, _RimStrength, _SmoothNormals;
            float4 _BoxSize;
            float _EdgeWidth, _EdgeStrength;
            float _ScanLines, _NoiseScale, _Breathe;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 objPos : TEXCOORD3;
                float3 objNormal : TEXCOORD4;
            };

            #define TAU 6.2831853

            float hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float hash31(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float3 hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Множество Жюлиа. Возвращает -1 внутри множества, иначе сглаженное число итераций.
            float julia(float2 z, float2 c)
            {
                float it = 0.0;
                float r2 = dot(z, z);
                int maxIt = (int)_FractalIter;
                [loop]
                for (int k = 0; k < 64; k++)
                {
                    if (k >= maxIt || r2 > 256.0) break;
                    z = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y) + c;
                    r2 = dot(z, z);
                    it += 1.0;
                }
                if (it >= (float)maxIt) return -1.0;
                return it + 1.0 - log2(0.5 * log(max(r2, 1.0001)));
            }

            v2f vert(appdata v)
            {
                v2f o;
                float t = _Time.y * _Speed;

                // дыхание = равномерное масштабирование (углы куба не расходятся)
                float breathe = (sin(t * 2.0) * 0.5 + 0.5) * _Breathe * _Intensity;
                float3 vtx = v.vertex.xyz * (1.0 + breathe);

                o.pos = UnityObjectToClipPos(float4(vtx, 1));
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, float4(vtx, 1)).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objPos = v.vertex.xyz;
                o.objNormal = v.normal;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                // --- нормаль: смесь с направлением "от центра" -> скруглённый Fresnel без швов ---
                float3 center = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 nFlat = normalize(i.worldNormal);
                float3 nSmooth = normalize(i.worldPos - center);
                float3 n = normalize(lerp(nFlat, nSmooth, _SmoothNormals));
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // --- базовое освещение ---
                float2 uvAlbedo = TRANSFORM_TEX(i.uv, _MainTex);
                fixed4 tex = tex2D(_MainTex, uvAlbedo) * _BaseColor;
                float ndl = saturate(dot(nFlat, _WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                float3 lightTerm = ndl * _LightColor0.rgb + ShadeSH9(float4(nFlat, 1));
                float3 lit = tex.rgb * lightTerm;

                // --- общее 3D-пространство анимации ---
                #if defined(_USE_WORLD_SPACE)
                    float3 P = i.worldPos;
                #else
                    float3 P = i.objPos;
                #endif
                float3 dir = normalize(_WaveDir.xyz + float3(1e-5, 0, 0));
                float proj = dot(P, dir);
                float s = proj * _WaveDir.w; // фаза волны

                // --- маски ---
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);

                float3 q = abs(i.objPos) / max(_BoxSize.xyz, 1e-4);
                float3 ea = smoothstep(1.0 - _EdgeWidth, 1.0, q);
                float edge = max(ea.x * ea.y, max(ea.y * ea.z, ea.x * ea.z));

                float2 uvMask = TRANSFORM_TEX(i.uv, _MaskTex);
                float3 glow = 0;

                #if defined(_MODE_PULSE)
                    float wave = 0.5 + 0.5 * sin(t * 2.5 - s * TAU);
                    float3 c = lerp(_GlowColor.rgb, _GlowColor2.rgb, 0.5 + 0.5 * sin(t * 0.8 + s * 3.14159));
                    glow = c * (rim * _RimStrength * (0.4 + wave)
                              + edge * _EdgeStrength * (0.5 + 0.5 * wave)
                              + pow(wave, 6.0) * 0.6);

                #elif defined(_MODE_SCANNER)
                    float y = frac(s - t * 0.35);
                    float band = pow(saturate(1.0 - abs(y - 0.5) * 2.0), 10.0);
                    float lines = 0.5 + 0.5 * sin(proj * _ScanLines - t * 6.0);
                    lines = pow(lines, 6.0) * 0.25;
                    glow = _GlowColor.rgb * (band * 3.0 + lines + rim * _RimStrength)
                         + _GlowColor2.rgb * edge * _EdgeStrength;

                #elif defined(_MODE_RAINBOW)
                    float hue = frac(s * 0.5 + t * 0.2 + rim * 0.3);
                    float3 rb = hsv2rgb(float3(hue, 0.85, 1.0));
                    float wave = 0.6 + 0.4 * sin(t * 3.0 - s * TAU);
                    glow = rb * (rim * _RimStrength * wave + edge * _EdgeStrength * 1.5 + 0.15 * wave);

                #elif defined(_MODE_GLITCH)
                    float step_t = floor(t * 8.0);
                    float row = floor(proj * _NoiseScale);
                    float n1 = hash21(float2(row, step_t));
                    float on = step(0.82, n1);
                    float shift = (hash21(float2(row, step_t + 7.0)) - 0.5) * 0.3 * on;

                    float2 uvG = uvAlbedo + float2(shift, 0);
                    float3 split;
                    split.r = tex2D(_MainTex, uvG + float2( 0.01 * on, 0)).r;
                    split.g = tex2D(_MainTex, uvG).g;
                    split.b = tex2D(_MainTex, uvG + float2(-0.01 * on, 0)).b;
                    lit = lerp(lit, split * _BaseColor.rgb * lightTerm, on * _Intensity);
                    uvMask += float2(shift, 0);

                    float flicker = 0.7 + 0.3 * step(0.5, hash11(step_t));
                    glow = _GlowColor.rgb * rim * _RimStrength * flicker
                         + _GlowColor2.rgb * (edge * _EdgeStrength + on * 1.2);

                #elif defined(_MODE_SPARKLE)
                    float3 g = P * _NoiseScale;
                    float3 id = floor(g);
                    float3 f = frac(g) - 0.5;
                    float h = hash31(id);
                    float d = length(f);
                    float twinkle = saturate(sin(t * 3.0 + h * TAU));
                    float sweep = 0.4 + 0.6 * (0.5 + 0.5 * sin(t * 2.0 - s * TAU));
                    float spark = smoothstep(0.35 * h, 0.0, d) * twinkle * step(0.55, h) * sweep;
                    float3 c = lerp(_GlowColor.rgb, _GlowColor2.rgb, h);
                    glow = c * (spark * 4.0 + rim * _RimStrength * 0.8 + edge * _EdgeStrength * 0.6);

                #elif defined(_MODE_SPIRAL)
                    // Винтовая спираль вокруг оси Wave Dir, проходит через все грани.
                    // На торцах куба сходится в центр "вертушкой".
                    float3 upv = abs(dir.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                    float3 ux = normalize(cross(dir, upv));
                    float3 wx = cross(dir, ux);
                    float3 O = i.objPos;
                    float ang = atan2(dot(O, wx), dot(O, ux));
                    float arms = floor(_Arms + 0.5);
                    float along = dot(O, dir) * _WaveDir.w * TAU;
                    float ph = arms * ang + along - t * 3.0;
                    float bandA = pow(0.5 + 0.5 * sin(ph), 6.0);
                    float bandB = pow(0.5 + 0.5 * sin(arms * ang - along * 0.5 + t * 2.0), 10.0) * 0.6;
                    glow = _GlowColor.rgb * (bandA * 3.0 + rim * _RimStrength)
                         + _GlowColor2.rgb * (bandB * 2.0 + edge * _EdgeStrength);

                #elif defined(_MODE_FRACTAL)
                    // Анимированное множество Жюлиа. Три проекции (YZ, XZ, XY) смешиваются по нормали,
                    // поэтому фрактал плавно переходит с грани на грань.
                    float3 O = i.objPos;
                    float3 wt = pow(abs(i.objNormal), 6.0);
                    wt /= (wt.x + wt.y + wt.z + 1e-5);
                    float ca = t * 0.3;
                    float2 jc = 0.7885 * float2(cos(ca), sin(ca));
                    float zoom = _FractalZoom * (1.0 + 0.15 * sin(t * 0.5));
                    float3 fcol = 0;
                    [unroll]
                    for (int k = 0; k < 3; k++)
                    {
                        float wgt = (k == 0) ? wt.x : ((k == 1) ? wt.y : wt.z);
                        if (wgt >= 0.02)
                        {
                            float2 p = (k == 0) ? O.yz : ((k == 1) ? O.xz : O.xy);
                            float v = julia(p * zoom, jc);
                            float3 fc;
                            if (v < 0.0)
                            {
                                fc = _GlowColor2.rgb * 0.5;
                            }
                            else
                            {
                                float3 pl = 0.5 + 0.5 * sin(v * 0.4 + t * 1.5 + float3(0, 2, 4));
                                float bright = saturate(v / (_FractalIter * 0.35));
                                fc = lerp(_GlowColor.rgb, _GlowColor2.rgb, pl.x) * (0.15 + 0.85 * bright) * (0.6 + 0.4 * pl.y);
                            }
                            fcol += fc * wgt;
                        }
                    }
                    glow = fcol * 2.0
                         + _GlowColor.rgb * rim * _RimStrength * 0.5
                         + _GlowColor2.rgb * edge * _EdgeStrength * 0.7;

                #elif defined(_MODE_PLASMA)
                    // Интерференция синусов в 3D + расходящиеся сферические кольца
                    float k = _WaveDir.w * TAU * 1.5;
                    float v = sin(P.x * k + t) + sin(P.y * k * 1.1 + t * 1.3)
                            + sin(P.z * k * 0.9 - t * 0.9) + sin(length(P) * k * 1.4 - t * 2.0 + s);
                    v *= 0.25;
                    float band = pow(0.5 + 0.5 * sin(v * TAU * 1.5 - t * 2.0), 3.0);
                    float3 pc = lerp(_GlowColor.rgb, _GlowColor2.rgb, 0.5 + 0.5 * sin(v * 3.0 + t));
                    glow = pc * (band * 2.2 + 0.2)
                         + _GlowColor.rgb * rim * _RimStrength * 0.7
                         + _GlowColor2.rgb * edge * _EdgeStrength * 0.5;

                #elif defined(_MODE_CELLS)
                    // 3D-Вороной: двигающиеся клетки, светятся границы и ядра
                    float3 gp = P * _CellScale;
                    float3 cid = floor(gp);
                    float3 cf = frac(gp);
                    float d1 = 8.0, d2 = 8.0, hCell = 0.0;
                    [unroll]
                    for (int zz = -1; zz <= 1; zz++)
                    [unroll]
                    for (int yy = -1; yy <= 1; yy++)
                    [unroll]
                    for (int xx = -1; xx <= 1; xx++)
                    {
                        float3 o3 = float3(xx, yy, zz);
                        float3 hh = hash33(cid + o3);
                        float3 pt = o3 + 0.5 + 0.45 * sin(t + TAU * hh);
                        float3 r = pt - cf;
                        float dd = dot(r, r);
                        if (dd < d1) { d2 = d1; d1 = dd; hCell = hh.x; }
                        else if (dd < d2) { d2 = dd; }
                    }
                    float f1 = sqrt(d1), f2 = sqrt(d2);
                    float border = 1.0 - smoothstep(0.0, 0.12, f2 - f1);
                    float fill = (0.5 + 0.5 * sin(t * 2.0 + hCell * TAU - s * TAU)) * (1.0 - saturate(f1));
                    float3 cc = lerp(_GlowColor.rgb, _GlowColor2.rgb, hCell);
                    glow = cc * (border * 3.0 + fill * 1.2)
                         + _GlowColor.rgb * rim * _RimStrength * 0.6
                         + _GlowColor2.rgb * edge * _EdgeStrength * 0.5;
                #endif

                #if defined(_PIXEL_BLINK)
                {
                    // Случайно мигающие "пиксели" в 3D-сетке (без швов между гранями).
                    // Вблизи к камере они невидимы, чем дальше - тем ярче проявляются.
                    float3 pxCell = floor(P * _PixelScale);
                    float pxH = hash31(pxCell);
                    float pxTime = t * _BlinkRate * (0.5 + pxH) + pxH * 17.0; // свой ритм у каждого пикселя
                    float pxLife = frac(pxTime);
                    float pxRand = hash21(float2(pxH * 91.7, floor(pxTime)));
                    float pxOn = step(1.0 - _PixelDensity, pxRand);          // загорается ли в этом цикле
                    float pxEnv = sin(pxLife * 3.14159);
                    pxEnv *= pxEnv;                                          // плавно вспыхивает и гаснет

                    float pxDist = distance(_WorldSpaceCameraPos, i.worldPos);
                    float pxVis = smoothstep(_PixelNear, max(_PixelFar, _PixelNear + 0.01), pxDist);

                    float3 pxCol = lerp(_GlowColor.rgb, _GlowColor2.rgb, hash31(pxCell + 3.7));
                    float3 pxGlow = pxCol * (pxOn * pxEnv * 3.0 * pxVis);
                    glow = lerp(glow, pxGlow, _PixelMix);
                }
                #endif

                // --- маска: светлые пиксели = свечение, тёмные = нет ---
                float m = dot(tex2D(_MaskTex, uvMask).rgb, float3(0.299, 0.587, 0.114));
                m = pow(saturate(m), _MaskPower);
                m = lerp(m, 1.0 - m, _MaskInvert);
                m = lerp(1.0, m, _MaskStrength);
                glow *= m * _Intensity;

                // --- композитинг ---
                // premult = то, что должно добавиться к экрану; цвет делим на альфу,
                // чтобы при любом бленде (Alpha / Additive) свечение не тускнело и не белело.
                float baseA = _BaseAlpha * tex.a;
                float3 premult = lit * baseA + glow;
                float glowAmt = saturate(max(glow.r, max(glow.g, glow.b)));
                float alpha = saturate(baseA + glowAmt);
                float3 col = premult / max(alpha, 1e-4);

                return fixed4(col, alpha * _Alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
