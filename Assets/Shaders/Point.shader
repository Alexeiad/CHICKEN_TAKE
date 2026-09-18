Shader "Custom/InformationOrb"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.1, 0.7, 1.0, 1.0)
        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 1.0, 1.0, 1.0)

        _GlowStrength ("Glow Strength", Range(0, 10)) = 3
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25

        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 10)) = 4

        _WaveSpeed ("Wave Speed", Range(0, 10)) = 2
        _WaveScale ("Wave Scale", Range(1, 30)) = 8

        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            fixed4 _BaseColor;
            fixed4 _EmissionColor;

            float _GlowStrength;
            float _PulseSpeed;
            float _PulseAmount;

            float _FresnelPower;
            float _RimStrength;

            float _WaveSpeed;
            float _WaveScale;

            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                o.viewDir = _WorldSpaceCameraPos - o.worldPos;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;

                // ---------------------------------------
                // Пульсация
                // ---------------------------------------

                float pulse =
                    sin(time * _PulseSpeed) * 0.5 + 0.5;

                pulse = lerp(
                    1.0 - _PulseAmount,
                    1.0 + _PulseAmount,
                    pulse
                );

                // ---------------------------------------
                // Fresnel по краям шара
                // ---------------------------------------

                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float fresnel =
                    pow(
                        1.0 - saturate(dot(normal, viewDir)),
                        _FresnelPower
                    );

                // ---------------------------------------
                // Бегущая энергетическая волна
                // ---------------------------------------

                float wave =
                    sin(
                        i.worldPos.y * _WaveScale
                        - time * _WaveSpeed
                    );

                wave = wave * 0.5 + 0.5;

                // Дополнительная мягкая модуляция
                wave = lerp(0.55, 1.35, wave);

                // ---------------------------------------
                // Цвет
                // ---------------------------------------

                float3 baseColor =
                    _BaseColor.rgb * wave;

                float3 emission =
                    _EmissionColor.rgb
                    * _GlowStrength
                    * pulse;

                // Свечение краёв
                emission +=
                    _EmissionColor.rgb
                    * fresnel
                    * _RimStrength;

                // ---------------------------------------
                // Итог
                // ---------------------------------------

                float brightness =
                    (0.35 + fresnel * 1.5)
                    * pulse;

                float3 finalColor =
                    baseColor * brightness
                    + emission;

                float alpha =
                    saturate(
                        (0.25 + fresnel * 0.9)
                        * _Alpha
                    );

                return fixed4(finalColor, alpha);
            }

            ENDCG
        }
    }

    FallBack "Transparent/VertexLit"
}