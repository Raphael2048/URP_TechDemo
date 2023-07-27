Shader "Hidden/Universal Render Pipeline/CartoonPP"
{
    Properties
    {
        [HideInInspector]_BlitTexture ("BlitTexture", 2D) = "white" { }
    }
    
    HLSLINCLUDE
        #pragma enable_d3d11_debug_symbols
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BlitTexture);
        SAMPLER(sampler_BlitTexture);
        float4 _BlitTexture_TexelSize;

        float HalfWidth;
    
    
        struct Attributes
            {
                uint vertexID : VERTEXID_SEMANTIC;
            };

            struct Varyings
            {
                float2 uv        : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.vertex = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }
            
            half distance(half3 A, half3 B)
            {
                half3 D = A - B;
                return dot(D, D);
            }
    
            #define HALF_WIDTH 4
            half4 snn(Varyings input) : SV_Target
            {
                half4 sum = 0;;
                half3 C = _BlitTexture.SampleLevel(sampler_PointClamp, input.uv, 0, int2(0, 0));
                for(int j = 0; j <= HALF_WIDTH; ++j)
                {
                    for(int i = -HALF_WIDTH; i <= HALF_WIDTH; ++i)
                    {
                        if(j == 0 && i <= 0) continue;
                        half4 c1 = _BlitTexture.SampleLevel(sampler_PointClamp, input.uv + float2(i, j) * _BlitTexture_TexelSize.xy, 0);
                        half4 c2 = _BlitTexture.SampleLevel(sampler_PointClamp, input.uv + -float2(i, j) * _BlitTexture_TexelSize.xy, 0);
                        half d1 = distance(c1, C);
                        half d2 = distance(c2, C);
                        if(d1 < d2)
                        {
                            sum.rgb += c1;
                        }
                        else
                        {
                            sum.rgb += c2;
                        }
                        sum.a += 1.0f;
                    }
                }
                return half4(sum.rgb / sum.a, 1.0f);
            }
    
    
            half4 kuwahara(Varyings input) : SV_Target
            {
                half3 col = 0;
                half min_sigma = 100.0f;

                half inv_n = 1.0f / ((HALF_WIDTH + 1) * (HALF_WIDTH + 1));

                int2 scales[4] = {
                    int2(1, 1),
                    int2(-1, 1),
                    int2(1, -1),
                    int2(-1, -1)
                };
                
                for(int k = 0; k < 4; ++k)
                {
                    float2 scale = scales[k];
                    half3 m = 0;
                    half3 s = 0; 
                    for(int j = 0; j <= HALF_WIDTH; ++j)
                    { 
                        for(int i = 0; i <= HALF_WIDTH; ++i)
                        {
                            half3 C = _BlitTexture.SampleLevel(sampler_PointClamp, input.uv + float2(i, j) * scale * _BlitTexture_TexelSize.xy, 0);
                            m += C;
                            s += C * C;
                        }
                    }
                    m *= inv_n;
                    s = abs(s * inv_n - m * m);
                    half sigma2 = s.x + s.y + s.z;
                    if(sigma2 < min_sigma)
                    {
                        min_sigma = sigma2;
                        col = m;
                    }
                }
                return half4(col, 1);
            }
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull OFF ZTest Always
        LOD 200

        Pass
        {
            Name "SNN"
            
            HLSLPROGRAM 
                #pragma vertex vert
                #pragma fragment snn
            ENDHLSL
        }
        
        Pass
        {
            Name "kuwahara"
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment kuwahara
            ENDHLSL
        }
    }
}
