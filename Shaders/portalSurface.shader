Shader "Unlit/portalSurface"
{
    Properties
    {
        [Header(Portal Settings)]
        _MainTex ("Portal Texture (RT)", 2D) = "white" {}
        
        [Header(Distortion Settings)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _DistortStrength ("Distort Strength", Range(0, 0.5)) = 0.1
        
        [Header(Advanced Distortion)]
        _Speed1 ("Layer 1 Speed (Slow)", Float) = 0.5
        _Speed2 ("Layer 2 Speed (Fast)", Float) = 1.5
        _PulseFrequency ("Pulse Frequency (Sine)", Float) = 2.0
        
        [Header(Fresnel Edge Settings)]
        [HDR] _FresnelColor ("Fresnel Color (HDR)", Color) = (0, 2, 5, 1)
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 10)) = 2.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "PortalForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha 
            ZWrite Off 
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 补上 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            // 用 URP 规范声明贴图和采样器
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            // 把所有属性放进 CBUFFER（必须包含 _NoiseTex_ST）
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float _DistortStrength;
                float _Speed1;
                float _Speed2;
                float _PulseFrequency;
                float4 _FresnelColor;
                float _FresnelPower;
                float _FresnelIntensity;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target // 改成 half4
            {
                // 6.1 心跳脉冲
               float pulse = sin(_Time.y * _PulseFrequency) * 0.5 + 0.5;
               
               // 6.2 动态强度
               float strength = _DistortStrength * lerp(0.2, 1.0, pulse);

                // 6.3 FBM 多层噪声叠加
                float2 uv1 = i.uv * _NoiseTex_ST.xy + _Time.y * _Speed1;
                float2 uv2 = i.uv * _NoiseTex_ST.zw * 2.3 - _Time.y * _Speed2;
                
                float4 noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1);
                float4 noise2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2);

                // 混合两层噪声
                float2 noise = (noise1.rg + noise2.rg) / 2;

                // 6.4 域扭曲 (Domain Warping)
                float2 distortOffset = (noise * 2.0 - 1.0) * strength;
                float2 distortedUV = i.uv + distortOffset;

                // 6.5 采样主画面
                half4 portalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);

                // 6.6 菲涅尔边缘光
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float3 normalWS = normalize(i.normalWS);
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _FresnelPower);

                // 边缘光呼吸
                half3 fresnelGlow = _FresnelColor.rgb * fresnel * _FresnelIntensity * lerp(0.5, 1.5, pulse);

                // 6.7 混合输出
                half3 finalColor = portalColor.rgb + fresnelGlow;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}