Shader "Hidden/Custom/WarpEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Intensity;
            float _Aberration;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 1. 计算径向模糊的 UV 偏移（从屏幕中心向四周发散）
                float2 center = float2(0.5, 0.5);
                float2 dist = i.uv - center;
                float2 blurUV = i.uv - dist * _Intensity;

                // 2. 计算 RGB 色差（分离红蓝通道）
                float2 redUV = blurUV + dist * _Aberration;
                float2 blueUV = blurUV - dist * _Aberration;

                // 3. 采样
                half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blurUV).rgb;
                col.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, redUV).r;
                col.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, blueUV).b;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}