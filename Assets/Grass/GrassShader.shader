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
        _MainTex          ("Texture (opt)",     2D)         = "white" {}
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
                float4 posOS    : POSITION;
                float3 normalOS : NORMAL;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct Varyings
            {
                float4 posCS     : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 color     : COLOR;
                float3 worldPos  : TEXCOORD2;
                float  fogFactor : TEXCOORD1;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _TranslucentColor;
                float  _TranslucentStr;
                float  _AOStrength;
                float  _AlphaCutoff;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float windMask  = IN.color.r; // 0 = base fija, 1 = punta libre
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

                // ?? Interacción con ovejas/jugador ???????????????
               // ?? Interacción con ovejas/jugador ???????????????
float3 interactOffset = float3(0, 0, 0);

for (int i = 0; i < _GrassInteractorCount; i++)
{
    float3 interactorPos = _GrassInteractors[i].xyz;
    float  interactorRad = abs(_GrassInteractors[i].w);

    if (interactorRad < 0.001) continue;

    float2 diff = worldPos.xz - interactorPos.xz;
    float  dist = length(diff);

    float influence  = 1.0 - smoothstep(0.0, interactorRad, dist);

    float2 pushDir     = dist > 0.05 ? diff / dist : float2(0, 0);
    float  centerAmount = 1.0 - smoothstep(0.0, interactorRad * 0.4, dist);

    // ? Altura real de la brizna guardada en vertex color B
    float bladeHeight = IN.color.b;

    // Empuje lateral en el borde
    float3 lateralPush = float3(pushDir.x, 0, pushDir.y)
                       * influence * (1.0 - centerAmount) * 0.3 * windMask;

    // ? Aplastamiento vertical escalado con la altura de la brizna
    // bladeHeight garantiza que briznas altas se aplasten completamente
    float3 verticalCrush = float3(0, -1, 0)
                         * centerAmount * influence * bladeHeight * windMask;

    interactOffset += lateralPush + verticalCrush;
}

                float4 posWS  = mul(UNITY_MATRIX_M, IN.posOS);
                posWS.xyz    += windOffset + interactOffset;

                OUT.posCS    = mul(UNITY_MATRIX_VP, posWS);
                OUT.uv       = IN.uv;
                OUT.color    = IN.color;
                OUT.worldPos = posWS.xyz;
                OUT.fogFactor = ComputeFogFactor(OUT.posCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, float facing : VFACE) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _AlphaCutoff);

                // Silueta procedural
                float sideAlpha = smoothstep(0.0, 0.18, IN.uv.x)
                                * smoothstep(1.0, 0.82, IN.uv.x)
                                * smoothstep(0.0, 0.08, IN.uv.y);
                float tipAlpha  = smoothstep(1.0, 0.7, IN.uv.y);
                clip(sideAlpha * tipAlpha - 0.3);

                half4 col = lerp(_BaseColor, _TipColor, IN.uv.y);
                col *= tex;

                // Normal suavizada hacia arriba
                Light  mainLight = GetMainLight();
                float  NdotL     = saturate(dot(float3(0,1,0), mainLight.direction) * 0.5 + 0.5);
                col.rgb *= mainLight.color * NdotL;

                // Translucencia falsa
                float3 viewDir       = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float  translucentDot = saturate(dot(-mainLight.direction, viewDir));
                float  translucentVal = pow(translucentDot, 2.0) * _TranslucentStr;
                float  backfaceTrans  = (facing < 0 ? 0.4 : 0.0) * _TranslucentStr;
                col.rgb += _TranslucentColor.rgb * mainLight.color
                         * max(translucentVal, backfaceTrans)
                         * IN.uv.y;

                // AO en la base
                float ao = lerp(1.0 - _AOStrength, 1.0, IN.uv.y);
                col.rgb *= ao;

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