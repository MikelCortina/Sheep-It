Shader "Custom/GrassURP"
{
    Properties
    {
        _BaseColor        ("Base Color",        Color)      = (0.3, 0.7, 0.2, 1)
        _TipColor         ("Tip Color",         Color)      = (0.8, 0.95, 0.3, 1)
        _TranslucentColor ("Translucent Color", Color)      = (0.6, 0.9, 0.1, 1)
        _TranslucentStr   ("Translucency",      Range(0,1)) = 0.5
        _AOStrength       ("AO Base Darkness",  Range(0,1)) = 0.6
        _AlphaCutoff      ("Alpha Cutoff",      Range(0,1)) = 0.1
        _NoiseScale       ("Color Noise Scale", Float)      = 0.15
        _NoiseStrength    ("Color Noise Str",   Range(0,1)) = 0.15
        _SpecularColor    ("Specular Color",    Color)      = (0.8, 1.0, 0.5, 1)
        _SpecularStr      ("Specular Strength", Range(0,1)) = 0.3
        _SpecularPower    ("Specular Power",    Range(1,64)) = 16
        _MainTex          ("Texture (opt)",     2D)         = "white" {}
        _NormalMap        ("Normal Map",        2D)         = "bump" {}
        _NormalStrength   ("Normal Strength",   Range(0,1)) = 0.5
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ?? Globals WindEmitter ??????????????????????????????
            float4 _GlobalWindDir;
            float  _GlobalWindStrength;
            float4 _GlobalWindOrigin;
            float  _GlobalWindSpeed;
            float  _GlobalWindFrequency;
            float  _GlobalTurbulence;

            // ?? Globals Interactores ?????????????????????????????
            float4 _GrassInteractors[10];
            int    _GrassInteractorCount;

            struct Attributes
            {
                float4 posOS     : POSITION;
                float3 normalOS  : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv        : TEXCOORD0;
                float2 uv2       : TEXCOORD1; // tinte RG
                float4 uv3       : TEXCOORD2; // tinte B + world XZ para noise
                float4 color     : COLOR;     // R=windMask, G=randomSeed, B=height
            };

            struct Varyings
            {
                float4 posCS     : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 tint      : TEXCOORD1; // RGB completo del tinte
                float2 worldXZ   : TEXCOORD2; // world XZ para noise
                float4 color     : TEXCOORD3;
                float3 worldPos  : TEXCOORD4;
                float3 worldNorm : TEXCOORD5;
                float  fogFactor : TEXCOORD6;
            };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _TranslucentColor;
                float4 _SpecularColor;
                float  _TranslucentStr;
                float  _AOStrength;
                float  _AlphaCutoff;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _SpecularStr;
                float  _SpecularPower;
                float  _NormalStrength;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
            CBUFFER_END

            // ?? Hash noise simple ????????????????????????????????
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash2(i              ).x;
                float b = hash2(i + float2(1,0)).x;
                float c = hash2(i + float2(0,1)).x;
                float d = hash2(i + float2(1,1)).x;

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float  windMask = IN.color.r;
                float3 worldPos = mul(UNITY_MATRIX_M, IN.posOS).xyz;

                // ?? Viento ???????????????????????????????????????
                float3 windDir = _GlobalWindDir.xyz;
                float  windLen = length(windDir);
                windDir = windLen > 0.001 ? windDir / windLen : float3(1, 0, 0);

                float3 origin        = _GlobalWindOrigin.xyz;
                float  distAlongWind = dot(worldPos - origin, windDir);
                float  phase         = distAlongWind / max(_GlobalWindSpeed, 0.01);
                float  time          = _Time.y * _GlobalWindFrequency;

                float wave1 = sin(time - phase);
                float wave2 = sin((time - phase) * 2.7 + 0.8) * 0.25;
                float wave3 = sin((time - phase) * 0.4       ) * 0.4;

                float3 windOffset = float3(windDir.x, 0.0, windDir.z)
                                  * (wave1 + wave2 + wave3)
                                  * _GlobalWindStrength * windMask;

                // ?? Interacción ??????????????????????????????????
                float3 interactOffset = float3(0, 0, 0);
                for (int i = 0; i < _GrassInteractorCount; i++)
                {
                    float3 interactorPos = _GrassInteractors[i].xyz;
                    float  interactorRad = abs(_GrassInteractors[i].w);
                    if (interactorRad < 0.001) continue;

                    float2 diff         = worldPos.xz - interactorPos.xz;
                    float  dist         = length(diff);
                    float  influence    = 1.0 - smoothstep(0.0, interactorRad, dist);
                    float2 pushDir      = dist > 0.05 ? diff / dist : float2(0, 0);
                    float  centerAmount = 1.0 - smoothstep(0.0, interactorRad * 0.4, dist);
                    float  bladeHeight  = IN.color.b;

                    float3 lateralPush   = float3(pushDir.x, 0, pushDir.y)
                                        * influence * (1.0 - centerAmount) * 0.3 * windMask;
                    float3 verticalCrush = float3(0, -1, 0)
                                        * centerAmount * influence * bladeHeight * windMask;
                    interactOffset += lateralPush + verticalCrush;
                }

                float4 posWS  = mul(UNITY_MATRIX_M, IN.posOS);
                posWS.xyz    += windOffset + interactOffset;

                OUT.posCS    = mul(UNITY_MATRIX_VP, posWS);
                OUT.uv       = IN.uv;
                OUT.tint     = float3(IN.uv2.x, IN.uv2.y, IN.uv3.x); // RGB tinte completo
                OUT.worldXZ  = IN.uv3.zw;                              // world XZ para noise
                OUT.color    = IN.color;
                OUT.worldPos = posWS.xyz;
                OUT.worldNorm = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(OUT.posCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, float facing : VFACE) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _AlphaCutoff);

                // ?? Silueta procedural ???????????????????????????
                float sideAlpha = smoothstep(0.0, 0.18, IN.uv.x)
                                * smoothstep(1.0, 0.82, IN.uv.x)
                                * smoothstep(0.0, 0.08, IN.uv.y);
                float tipAlpha  = smoothstep(1.0, 0.7, IN.uv.y);
                clip(sideAlpha * tipAlpha - 0.3);

                // ?? 1. Color base con tinte de zona ?????????????
                half4 col = lerp(_BaseColor, _TipColor, IN.uv.y);
                col.rgb   = lerp(col.rgb, IN.tint.rgb, 0.55);
                col      *= tex;

                // ?? 6. Normal map ????????????????????????????????
                half3  normalTS  = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                float3 worldNorm = normalize(lerp(
                    float3(0, 1, 0),
                    IN.worldNorm + normalTS.xyz,
                    _NormalStrength));
                worldNorm *= (facing < 0 ? -1.0 : 1.0);

                // ?? 5. Iluminación con normal suavizada ??????????
                Light  mainLight = GetMainLight();
                float  NdotL     = saturate(dot(worldNorm, mainLight.direction) * 0.5 + 0.5);
                col.rgb *= mainLight.color * NdotL;

                // ?? 2. Translucencia falsa ???????????????????????
                float3 viewDir        = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float  translucentDot = saturate(dot(-mainLight.direction, viewDir));
                float  translucentVal = pow(translucentDot, 2.0) * _TranslucentStr;
                float  backfaceTrans  = (facing < 0 ? 0.4 : 0.0) * _TranslucentStr;
                col.rgb += _TranslucentColor.rgb * mainLight.color
                         * max(translucentVal, backfaceTrans)
                         * IN.uv.y;

                // ?? AO en la base ????????????????????????????????
                float ao = lerp(1.0 - _AOStrength, 1.0, IN.uv.y);
                col.rgb *= ao;

                // ?? 7. Ruido de color en world space ????????????
                float noise = valueNoise(IN.worldXZ * _NoiseScale) * 2.0 - 1.0;
                col.rgb    += noise * _NoiseStrength;

                // ?? 8. Specular en puntas ????????????????????????
                float3 halfDir  = normalize(mainLight.direction + viewDir);
                float  NdotH    = saturate(dot(worldNorm, halfDir));
                float  specular = pow(NdotH, _SpecularPower) * _SpecularStr;
                float  tipMask  = smoothstep(0.6, 1.0, IN.uv.y);
                col.rgb        += _SpecularColor.rgb * specular * tipMask;

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }

        // ?? ShadowCaster ?????????????????????????????????????????????????????????
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float     _AlphaCutoff;

            struct AttributesShadow { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct VaryingsShadow   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryingsShadow vertShadow(AttributesShadow IN)
            {
                VaryingsShadow OUT;
                OUT.posCS = UnityObjectToClipPos(IN.posOS);
                OUT.uv    = IN.uv;
                return OUT;
            }

            half4 fragShadow(VaryingsShadow IN) : SV_Target
            {
                clip(tex2D(_MainTex, IN.uv).a - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}