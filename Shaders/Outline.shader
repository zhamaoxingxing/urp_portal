Shader "Portals/Outline"
{
    Properties
    {
        // HDR 颜色：允许你调出极其耀眼的颜色
        [HDR] _OutlineColour("Outline Colour", Color) = (0, 2, 5, 1)
        
        // 发光强度倍数
        _GlowIntensity("Glow Intensity", Range(1, 10)) = 2.0
        
        // 呼吸脉冲速度
        _PulseSpeed("Pulse Speed", Float) = 2.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry+2"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        ENDHLSL
        
        Stencil
        {
            Ref 0
            Comp equal
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColour;
                float _GlowIntensity;
                float _PulseSpeed;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // 制造一个基于时间的呼吸心跳 (0 到 1 之间)
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                
                // 让亮度在基础的 80% 到 120% 之间随心跳起伏
                float dynamicIntensity = _GlowIntensity * lerp(0.8, 1.2, pulse);

                // 【核心修复】：直接返回面板上选的颜色，乘以动态强度。
                // 不再做任何奇奇怪怪的色彩矩阵变换。
                return _OutlineColour * dynamicIntensity;
            }
            ENDHLSL
        }
    }
}