Shader "Custom/GrassURP"
{
    Properties
    {
        _BaseColor   ("Base Color",   Color)      = (0.3, 0.7, 0.2, 1)
        _TipColor    ("Tip Color",    Color)      = (0.8, 0.95, 0.3, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        _MainTex     ("Texture (opt)", 2D)        = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline"
               "RenderType"="TransparentCutout"
               "Queue"="AlphaTest" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ? Globals FUERA del CBUFFER (SRP Batcher requiere esto)
            float4 _GlobalWindDir;
            float  _GlobalWindStrength;
            float4 _GlobalWindOrigin;
            float  _GlobalWindSpeed;
            float  _GlobalWindFrequency;
            float  _GlobalTurbulence;

            struct Attributes
            {
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float2 uv       : TEXCOORD0;
                // R = windMask (0 base, 1 punta)
                // G = phaseSeed (no usado en viento global, reservado)
                // A = siempre 1
                float4 color    : COLOR;
            };

            struct Varyings
            {
                float4 posCS     : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 color     : COLOR;
                float  fogFactor : TEXCOORD1;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // ? Solo propiedades del material dentro del CBUFFER
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float  _AlphaCutoff;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float windMask = IN.color.r; // 0 = base fija, 1 = punta libre

                // Posición world del vértice
                float3 worldPos = mul(UNITY_MATRIX_M, IN.posOS).xyz;

                // Dirección del viento normalizada de forma segura
                float3 windDir = _GlobalWindDir.xyz;
                float  windLen = length(windDir);
                windDir = windLen > 0.001 ? windDir / windLen : float3(1, 0, 0);

                // Fase: briznas más lejos en la dirección del viento se doblan después
                float3 origin        = _GlobalWindOrigin.xyz;
                float  distAlongWind = dot(worldPos - origin, windDir);
                float  phase         = distAlongWind / max(_GlobalWindSpeed, 0.01);

                float time = _Time.y * _GlobalWindFrequency;

                // Tres ondas para movimiento orgánico
                float wave1 = sin(time - phase);
                float wave2 = sin((time - phase) * 2.7 + 0.8) * 0.25;
                float wave3 = sin((time - phase) * 0.4       ) * 0.4;

                float totalWave  = (wave1 + wave2 + wave3) * _GlobalWindStrength * windMask;
                float3 windOffset = float3(windDir.x, 0.0, windDir.z) * totalWave;

                float4 posWS  = mul(UNITY_MATRIX_M, IN.posOS);
                posWS.xyz    += windOffset;

                OUT.posCS     = mul(UNITY_MATRIX_VP, posWS);
                OUT.uv        = IN.uv;
                OUT.color     = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.posCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Degradado base ? punta por UV.y
                half4 col = lerp(_BaseColor, _TipColor, IN.uv.y);
                col *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // ? Alpha cutoff bajo (0.1) para que no corte geometry válida
                clip(col.a - _AlphaCutoff);

                // Iluminación difusa simple
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(float3(0,1,0), mainLight.direction) * 0.5 + 0.5);
                col.rgb *= mainLight.color * NdotL;

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}