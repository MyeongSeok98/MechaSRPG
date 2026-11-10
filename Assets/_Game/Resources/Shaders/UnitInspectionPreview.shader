Shader "Hidden/MechaXCOM/UnitInspectionPreview"
{
    Properties
    {
        _MainTex("Model texture", 2D) = "white" {}
        _Color("Model color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normal = mul((float3x3)UNITY_MATRIX_V, UnityObjectToWorldNormal(input.normal));
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * _Color;
                color.rgb *= .45 + .55 * saturate(dot(normalize(input.normal), normalize(float3(.3,.6,1))));
                return color;
            }
            ENDHLSL
        }
    }
}
