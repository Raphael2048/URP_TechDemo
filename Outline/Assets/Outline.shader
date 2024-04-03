Shader "OutlineShader"
{
    Properties
    {
        _OutlineColor("Outline Color", Color)=(1,1,1,1)
        _OutlineSize("OutlineSize", Range(0,0.01))=0.001
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex:POSITION;
                float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
            };
            struct v2f
            {
                float4 clipPos:SV_POSITION;
                float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
            };
            v2f vert (appdata v)
            {
                v2f o;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal( v.normal);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                float3 cameraDirection = UNITY_MATRIX_V[2].xyz;
                float c = dot(cameraDirection, i.normal);
                return float4(c, c, c, 1);
            }
            ENDCG
        }
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _OutlineColor;
            float _OutlineSize;
            struct appdata
            {
                float3 vertex:POSITION;
                float3 normal:NORMAL;
                float3 tangent :TANGENT;
                float2 uv3:TEXCOORD3;
                float2 uv4:TEXCOORD4;
            };
            struct v2f
            {
                float4 clipPos:SV_POSITION;
            };
            v2f vert (appdata v)
            {
                v2f o;
                float3 normal = float3(v.uv3.xy, v.uv4.x);
                float3 bitangent = cross(v.normal, v.tangent);
                float3x3 tangentToWorld = float3x3(v.tangent, bitangent, v.normal);
                normal = mul(normal, tangentToWorld);
 
                // normal = UnityObjectToWorldDir(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex, 1.0));
                worldPos += normal * _OutlineSize;
                o.clipPos = UnityWorldToClipPos(worldPos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
