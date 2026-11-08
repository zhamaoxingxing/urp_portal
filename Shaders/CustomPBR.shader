Shader "Unlit/CustomPBR"
{
    Properties
    {
        _BaseMap ("Albedo (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0
        
        _Roughness ("Base Roughness", Range(0, 1)) = 0.5
        _RoughnessMap ("Roughness Map", 2D) = "white" {}

        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _EnvIntensity ("Environment Intensity", Range(0, 2)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"  "RenderPipeline" = "UniversalRenderPipeline"    }
        LOD 100

        Pass
        {   Tags { "LightMode"="UniversalForward"}//光照模式前向渲染

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS//关闭 开启阴影
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE //级联阴影
        
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _NormalMap_ST;     // 加上这个
            float4 _RoughnessMap_ST;  // 加上这个
            half4 _BaseColor;
            float _NormalScale;
            float _Roughness;
            float _Metallic;
            float _EnvIntensity;
            CBUFFER_END 
            
            //高光brdf函数 fc  
            
            //d:
            float DistributionGGX(float NoH,float roughness)
            {
                float a = roughness * roughness; // a = 粗糙度的平方
                float a2 = a * a;                // a2 = 粗糙度的四次方
                float NoH2 = NoH * NoH;
                float x = NoH2 * (a2 - 1.0) + 1.0;
                return a2 / (PI * x * x + 0.0001);
            }

            //f: 
            float3 FresnelSchlick(float HoV,float3 F0)
             {
                  return F0 + (1 - F0) * pow(1.0 - HoV, 5);
             }

             //g:
             float Geometry(float x, float k) 
            {
                return x / (x * (1 - k) + k);
            }

            float GeometrySmith(float NoV, float NoL, float roughness)
            { // 计算直接光照专用的 k
                float r = (roughness + 1.0);
                float k = (r * r) / 8.0; 
                return Geometry(NoV, k) * Geometry(NoL, k);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                
                // 你之前的写法
                o.bitangentWS = cross( o.tangentWS,o.normalWS) * v.tangentOS.w;
                
                return o;
            }

          half4 frag (v2f i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                float2 normalUV = TRANSFORM_TEX(i.uv, _NormalMap);
                half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV);
                half3 normalTS = UnpackNormalScale(normal, _NormalScale);
                
                float2 roughnessUV = TRANSFORM_TEX(i.uv, _RoughnessMap);
                float roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, roughnessUV).r * _Roughness;
                roughness = max(roughness, 0.002);

                // TBN + 法线贴图
                half3 T = normalize(i.tangentWS);
                half3 B = normalize(i.bitangentWS);
                half3 Ngeo = normalize(i.normalWS);
                half3x3 TBN = half3x3(T, B, Ngeo);
                half3 N = normalize(TransformTangentToWorld(normalTS, TBN));

                half3 V = normalize(GetCameraPositionWS() - i.positionWS);
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                // 你实测出来的正确方向
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(V + L);

                float NoH = max(dot(N, H), 0.0);
                float NoV = max(dot(N, V), 0.0);
                float NoL = max(dot(N, L), 0.0);
                float HoV = max(dot(H, V), 0.0);

                float metallic = _Metallic;
                float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo.rgb, metallic);

                // 直接光 BRDF
                float d = DistributionGGX(NoH, roughness);
                float3 f = FresnelSchlick(HoV, F0);
                float g = GeometrySmith(NoV, NoL, roughness);

                float3 fc = (d * f * g) / (4 * NoL * NoV + 0.000001);
                float3 ks = f;
                float3 kd = (1 - ks) * (1 - metallic);
                float3 fl = albedo.rgb / PI;

                float3 light = mainLight.color * NoL * mainLight.shadowAttenuation;
                float3 finalColor = (kd * fl + fc) * light;

                // ================== 间接光 ==================
                half3 ambient = SampleSH(N);
                // 补上 /PI 和 (1-metallic)
                half3 kdi = 1.0 - (1.0 + F0) * pow(1.0 - NoV, 5.0);
                half3 indirectDiffuse = ambient * albedo.rgb * kdi * (1 - metallic); 
              
                // 间接高光
                float mipLevel = roughness * 7.0; 
                float3 reflectDir = reflect(-V, N);
              
               half4 envCubeSample = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, mipLevel);

                half3 envSpecColor = DecodeHDREnvironment(envCubeSample, unity_SpecCube0_HDR);
              
                float surfaceReduction = 1.0 / (roughness * roughness + 1.0);
                float3 grazingTerm = saturate((1 - roughness) + F0); 
                float t = pow(1 - NoV, 5);
                float3 IndirF0 = lerp(F0, grazingTerm, t);
                half3 indirectSpecular = envSpecColor * surfaceReduction * IndirF0;
              
                float3 indirectTotal = (indirectDiffuse + indirectSpecular) * _EnvIntensity;
                finalColor = finalColor + indirectTotal;
     
                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }

       Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // 严格按照这个顺序引入，缺一不可
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}