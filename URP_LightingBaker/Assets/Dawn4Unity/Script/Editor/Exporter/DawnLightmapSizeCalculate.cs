using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
    public partial class DawnExporter
    {
        static int CalculateLightmapSize(float InScaleInLightmap, float InSurfaceArea, float InTexelPerUnit, Rect InUVBounds,bool bUseLargeSize = false)
        {
            if (bUseLargeSize)
            {
                return (int)CalculateLightmapSizeLarge(InScaleInLightmap, InSurfaceArea, InTexelPerUnit, InUVBounds);
            }
            return (int)CalculateLightmapSizeNormal(InScaleInLightmap, InSurfaceArea, InTexelPerUnit,InUVBounds);
        }

        static float CalculateLightmapSizeNormal(float InScaleInLightmap, float InSurfaceArea, float InTexelPerUnit, Rect InUVBounds)
        {
            float TexelPerUnit = Mathf.Max(0.0f, InTexelPerUnit * InScaleInLightmap);

            float UVSize = InUVBounds.width * InUVBounds.height;

            float NormalizedToWorldScale = Mathf.Sqrt(InSurfaceArea / UVSize);

            float TexelSize = TexelPerUnit * Mathf.Max(InUVBounds.height, InUVBounds.width) * NormalizedToWorldScale;

            return TexelSize;
        }

        static float CalculateLightmapSizeLarge(float InScaleInLightmap, float InSurfaceArea, float InTexelPerUnit, Rect InUVBounds)
        {
            float TexelPerUnit = InTexelPerUnit;

            float UVSize = InUVBounds.width * InUVBounds.height;

            float NormalizedToWorldScale = Mathf.Sqrt(InScaleInLightmap * InSurfaceArea * 2 / UVSize);

            float TexelSize = TexelPerUnit * Mathf.Max(InUVBounds.height, InUVBounds.width) * NormalizedToWorldScale;

            return TexelSize;
        }

        static Rect ToRect(Vector4 Input)
        {
            return new Rect(Input.x,Input.y,Input.z - Input.x,Input.w - Input.y);
        }
    }
}