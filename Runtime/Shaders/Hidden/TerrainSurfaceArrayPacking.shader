Shader "Hidden/Terrain Tools/Terrain Surface Array Packing"
{
    Properties
    {
        _SourceAlbedo("Albedo", 2D) = "gray" {}
        _SourceNormal("Normal", 2D) = "bump" {}
        _SourceMask("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "UnityCG.cginc"

        sampler2D _SourceAlbedo;
        sampler2D _SourceNormal;
        sampler2D _SourceMask;
        float _HasNormal;
        float _HasMask;
        float4 _MaskRemapMin;
        float4 _MaskRemapMax;
        float4 _DiffuseRemapMin;
        float4 _DiffuseRemapMax;
        float _DefaultHeight;
        float _DefaultMetallic;
        float _DefaultOcclusion;
        float _DefaultSmoothness;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = UnityObjectToClipPos(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        float4 SampleRemappedMask(float2 uv)
        {
            float4 mask = tex2D(_SourceMask, uv);
            return lerp(_MaskRemapMin, _MaskRemapMax, mask);
        }
        ENDHLSL

        Pass
        {
            Name "AlbedoHeight"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragAlbedoHeight

            float4 FragAlbedoHeight(Varyings input) : SV_Target
            {
                // URP TerrainLit treats diffuseRemapMax.rgb as the layer tint.
                // TerrainLitShaderGUI keeps diffuseRemapMin.rgb at zero.
                float3 albedo = tex2D(_SourceAlbedo, input.uv).rgb * _DiffuseRemapMax.rgb;
                float height = _HasMask > 0.5 ? SampleRemappedMask(input.uv).b : _DefaultHeight;
                return float4(albedo, height);
            }
            ENDHLSL
        }

        Pass
        {
            Name "NormalSurface"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragNormalSurface

            float4 FragNormalSurface(Varyings input) : SV_Target
            {
                float3 normalTS = _HasNormal > 0.5
                    ? UnpackNormal(tex2D(_SourceNormal, input.uv))
                    : float3(0.0, 0.0, 1.0);
                float4 mask = _HasMask > 0.5
                    ? SampleRemappedMask(input.uv)
                    : float4(_DefaultMetallic, _DefaultOcclusion, _DefaultHeight, _DefaultSmoothness);
                return float4(normalTS.xy * 0.5 + 0.5, mask.g, mask.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Metallic"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragMetallic

            float4 FragMetallic(Varyings input) : SV_Target
            {
                float metallic = _HasMask > 0.5 ? SampleRemappedMask(input.uv).r : _DefaultMetallic;
                return metallic.xxxx;
            }
            ENDHLSL
        }
    }
}
