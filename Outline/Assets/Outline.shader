Shader "OutlineShader"
{
    Properties
    {
        [Toggle] _ShowVertexControlValue("ShowVertexControlValue", Float)=0
        [Toggle] _ShowEncodeNormal("ShowEncodeNormal", Float) = 0
        _OutlineColor("Outline Color", Color)=(0, 0, 0, 1)
        _OutlineSize("OutlineSize", Range(0, 1)) = 0.1
        [Toggle] _FlattenNormal("FlattenNormal", Float) = 0
        [Toggle] _VertexControlWidth("VertexControlWidth", Float) = 0
        _DepthBias("Depth Bias", Float) = 0
        [Toggle] _VertexControlDepth("VertexControlDepthOffset", Float) =0
        _DepthOffsetScale("Depth Offset Scale", Float) = 1
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
                float4 color: COLOR;
            };
            struct v2f
            {
                float4 clipPos:SV_POSITION;
                float3 normal:NORMAL;
                float4 color:COLOR;
            };
            float _ShowVertexControlValue;
            float _ShowEncodeNormal;
            v2f vert (appdata v)
            {
                v2f o;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal( v.normal);
                o.color = v.color;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col;
                float3 cameraDirection = UNITY_MATRIX_V[2].xyz;
                if(_ShowVertexControlValue)
                {
                    return float4(i.color.x, i.color.y, 0, 1);   
                }
                else if (_ShowEncodeNormal)
                {
                    return float4(i.color.y, i.color.z, 0, 1);
                }
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
            
            float4 _OutlineColor;
            float _OutlineSize;
            float _FlattenNormal;
            float _VertexControlWidth;
            float _DepthBias;
            float _VertexControlDepth;
            float _DepthOffsetScale;
            
            struct appdata
            {
                float3 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent :TANGENT;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 clipPos:SV_POSITION;
            };

            float3 OctahedronToUnitVector( float2 Oct )
            {
	            float3 N = float3( Oct, 1 - dot(float2(1, 1), abs(Oct) ) );
	            if( N.z < 0 )
	            {
		            N.xy = ( 1 - abs(N.yx) ) * ( N.xy >= 0 ? float2(1,1) : float2(-1,-1) );
	            }
	            return normalize(N);
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex, 1.0));
                
                 float3 normal = OctahedronToUnitVector(v.color.ba * 2 - 1);
                float3 bitangent = cross(v.normal, v.tangent.xyz) * v.tangent.w;
                float3x3 tangentToLocal = float3x3(v.tangent.xyz, bitangent, v.normal);
                normal = mul(normal, tangentToLocal);
                
                normal = UnityObjectToWorldDir(normal);

                if(_FlattenNormal)
                {
                    float3 cameraDirection =  normalize(UNITY_MATRIX_V[2].xyz);
                    normal -= cameraDirection * dot(cameraDirection, normal);
                }
                if(_VertexControlWidth)
                {
                    normal *= v.color.x;
                }
                worldPos += normal * _OutlineSize * 0.01;
                
                float3 viewDirection = normalize(worldPos - _WorldSpaceCameraPos);
                worldPos += viewDirection * 0.01 * _DepthBias;
                if(_VertexControlDepth)
                {
                    worldPos += viewDirection * 0.01 * _DepthOffsetScale * (1 - v.color.y);   
                }
                
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
