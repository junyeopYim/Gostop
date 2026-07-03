Shader "Hwatu/InkDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Ink Mask", 2D) = "white" {}
        _Threshold ("Threshold", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.04
        _EdgeColor ("Edge Color", Color) = (0.09,0.07,0.06,1)
        _NoiseStrength ("Noise Strength", Range(0,0.2)) = 0.035
        _Invert ("Invert", Float) = 0
        _Saturation ("Saturation", Range(0,1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            fixed4 _EdgeColor;
            float4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Threshold;
            float _EdgeWidth;
            float _NoiseStrength;
            float _Invert;
            float _Saturation;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                // 마스크는 단채널 데이터: 그레이(R=G=B) 절차 마스크든 R8 잉크순서 마스크든
                // 값은 .r에 있다 (기존 그레이 마스크는 dot(rgb,luma)와 동일값).
                float mask = tex2D(_MaskTex, IN.texcoord).r;
                mask = lerp(mask, 1.0 - mask, step(0.5, _Invert));

                float rough = (hash21(IN.texcoord * 160.0) - 0.5) * _NoiseStrength;
                float cut = _Threshold + rough;
                float visible = step(mask, cut);
                color.a *= visible;

                float edge = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth * 2.0 + 0.0001, abs(mask - cut));
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, saturate(edge) * visible * _EdgeColor.a);

                // 채색 스밈: 그려짐 1단계에서 _Saturation=0(먹 그레이) → 2단계 1(원본색).
                // 기본 1이므로 기존 경로는 원본색 그대로 (동작 불변).
                float3 gray = dot(color.rgb, float3(0.299, 0.587, 0.114)).xxx;
                color.rgb = lerp(gray, color.rgb, _Saturation);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
