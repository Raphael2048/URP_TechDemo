using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace IrradianceVolume
{
    static class BoxUtils
    {
        public static int IndexToId(int3 size, int3 index)
        {
            return (index.z * size.y + index.y) * size.x + index.x;
        }

        public static int3 IdToIndex(int3 size, int id)
        {
            return new int3(id % size.x, (id / size.x) % size.y, id / (size.x * size.y));
        }

        public static int IPow(int Base, int Exp)
        {
            int result = 1;
            for (;;)
            {
                if ((Exp & 1) != 0)
                    result *= Base;
                Exp >>= 1;
                if (Exp == 0)
                    break;
                Base *= Base;
            }

            return result;
        }

        public static uint PackInt4(int4 Num)
        {
            return ((uint)Num.x) + ((uint)Num.y << 8) + ((uint)Num.z << 16) + ((uint)Num.w << 24);
        }
        
        public static int Float2Norm(float v)
        {
            return (Mathf.Clamp(Mathf.RoundToInt(v * 255.0f), 0, 255));
        }
        
        public static readonly float kRGBMRange = 8.0f;
        public static uint EncodeRGBM(float3 color)
        {
            color /= kRGBMRange;
            float m = Mathf.Max(Mathf.Max(color.x, color.y), Mathf.Max(color.z, 1e-5f));
            color /= m;
            return BoxUtils.PackInt4(new int4(
                Float2Norm(color.x), Float2Norm(color.y), Float2Norm(color.z), Float2Norm(m)));
        }

        public struct RGB24
        {
            public char x, y, z;
        };

        public static RGB24 EncodeToRGB24(float3 sh2)
        {
            RGB24 result;
            result.x = (char)Float2Norm(sh2.x * 0.25f + 0.5f);
            result.y = (char)Float2Norm(sh2.y * 0.25f + 0.5f);
            result.z = (char)Float2Norm(sh2.z * 0.25f + 0.5f);
            return result;
        }
        
        public static uint EncodeSH2(float3 sh2)
        {
            int x = Float2Norm(sh2.x * 0.25f + 0.5f);
            int y = Float2Norm(sh2.y * 0.25f + 0.5f);
            int z = Float2Norm(sh2.z * 0.25f + 0.5f);
            return PackInt4(new int4(x, y, z, 0));
        }
    }

    [Serializable]
    public class IrradianceVolumeOctreeNode
    {
        public float3 OriginPos;
        public float3 ChildCellSize;
        public int3 Index;
        public int Depth;
        public bool HasChildren;
        public IrradianceVolumeOctreeNode[] Children;
        public IrradianceVolumeOctreeNode Parent;

        [NonSerialized]
        public float3[] SamplePositions;

        // 已知映射贴图在MappingDepth深度处一一映射(每个Node占据一个体素)，算出当前Node在MappingTexture中，占据的像素坐标范围
        public int3[] IndexesAtMappingTexture()
        {
            int MappingDepth = IrradianceVolumeOctree.MAX_REFINE_LEVEL - 1;
            if (Depth > MappingDepth)
            {
                Debug.LogError("XXX");
            }

            int3 ThisLevelIndex = Index;
            IrradianceVolumeOctreeNode Node = Parent;
            while (Node != null)
            {
                ThisLevelIndex += Parent.Index *
                                  BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES, Depth - Node.Depth);
                Node = Node.Parent;
            }

            int ThisLevelToMappingLevel = BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES, MappingDepth - Depth);
            int3[] indexes = new int3[ThisLevelToMappingLevel * ThisLevelToMappingLevel * ThisLevelToMappingLevel];
            for (int i = 0; i < ThisLevelToMappingLevel; ++i)
            {
                for (int j = 0; j < ThisLevelToMappingLevel; ++j)
                {
                    for (int k = 0; k < ThisLevelToMappingLevel; ++k)
                    {
                        indexes[
                            BoxUtils.IndexToId(
                                new int3(ThisLevelToMappingLevel, ThisLevelToMappingLevel, ThisLevelToMappingLevel),
                                new int3(i, j, k))] = ThisLevelIndex * ThisLevelToMappingLevel + new int3(i, j, k);
                    }
                }
            }

            return indexes;
        }

        // 算出当前Node，在MainBlock中的坐标
        public int3 IndexAtMainBlock()
        {
            return NodeAtTopLevel().Index;
        }

        public IrradianceVolumeOctreeNode NodeAtTopLevel()
        {
            IrradianceVolumeOctreeNode n = this;
            while (n.Parent != null)
            {
                n = n.Parent;
            }

            return n;
        }

        public void GetDebugPositions(int level, HashSet<Vector3> set)
        {
            if (level < Depth) return;
            if (SamplePositions != null)
            {
                for (int i = 0; i < SamplePositions.Length; ++i)
                {
                    int3 index =
                        BoxUtils.IdToIndex(
                            new int3(IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1,
                                IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1,
                                IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1), i);
                    set.Add(SamplePositions[i]);
                }
            }

            if (HasChildren && Children!= null)
            {
                foreach (var node in Children)
                {
                    if(node !=null) node.GetDebugPositions(level, set);
                }
            }
        }
    }


    [Serializable]
    public class IrradianceVolumeOctree : ScriptableObject
    {
        public static readonly int MAX_REFINE_LEVEL = 3;
        public static readonly int SUBDIVIDE_SLICES = 3;
        [SerializeField] public float3 OriginPos;

        [SerializeField] public float3 CellSize;
        [SerializeField] public int3 CellCount;
        [SerializeField] public Texture3D SHTexture1, SHTexture2, MappingTexture;
        [SerializeField] public int3 TextureSize;
        [SerializeField] public int3 MappingTextureSize;
        [SerializeField] public bool Saved = false;
        
        [NonSerialized] public IrradianceVolumeOctreeNode[] Nodes;
        [NonSerialized] public float3[] SamplePositions;
        [NonSerialized] public BVH Tree;


        //[SerializeField] public Texture3D Morning1on, Morning2on;
        //[SerializeField] public Texture3D Morning1off, Morning2off;
        //[SerializeField] public Texture3D Noon1, Noon2;
        //[SerializeField] public Texture3D Dusk1on, Dusk2on;
        //[SerializeField] public Texture3D Dusk1off, Dusk2off;
        //[SerializeField] public Texture3D Night1, Night2;
        [SerializeField] public List<Texture3D> textures1;
        [SerializeField] public List<Texture3D> textures2;
        public float MaxRepulseDistance()
        {
            return Mathf.Max(Mathf.Max(CellSize.x, CellSize.y), CellSize.z) / BoxUtils.IPow(SUBDIVIDE_SLICES,MAX_REFINE_LEVEL - 1);
        }

        public int IndexOf(int i, int j, int k)
        {
            return BoxUtils.IndexToId(CellCount, new int3(i, j, k));
        }

        public int IndexOf(int3 cellLocation)
        {
            return BoxUtils.IndexToId(CellCount, cellLocation);
        }

        public IrradianceVolumeOctreeNode GetNodeAtLocation(int i, int j, int k)
        {
            return Nodes[IndexOf(i, j, k)];
        }

        public IrradianceVolumeOctreeNode GetNodeAtLocation(int3 location)
        {
            return GetNodeAtLocation(location.x, location.y, location.z);
        }

        public void Get3DTextureParams(int3 BeginLocation, int3 TextureSize, out float3 k, out float3 b)
        {
            float3 box = CellSize * CellCount;
            k = CellCount / (TextureSize * box);
            b = (0.5f + new float3(BeginLocation)) / TextureSize - k * OriginPos;
        }

        // 将起始位置映射至 0，结束位置映射至 MappingTextureSize
        public void GetMappingTextureCoordParams(out float3 mul, out float3 add)
        {
            float3 box = CellSize * CellCount;
            mul = MappingTextureSize / box;
            add = -mul * OriginPos;
        }

        // 从MappingTexture到 SHTexture的最大Level，是一个等比缩放映射
        public float GetMappingTextureCoordToSHMainBlockTextureCoord()
        {
            return 1.0f / BoxUtils.IPow(SUBDIVIDE_SLICES, MAX_REFINE_LEVEL - 2);
        }
        
        private static bool USE_THREAD_POOL = true;

        private static readonly object Lock = new object();
        public void BestPositionOfRootNode(float3 pos, int index, Dictionary<Vector3, Vector3> positions)
        {
            if (USE_THREAD_POOL)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(obj =>
                {
                    Vector3 newPos;
                    if (positions.ContainsKey(pos))
                    {
                        newPos = positions[pos];
                    }
                    else
                    {
                        newPos = Tree.BestPlacePosition(pos, MaxRepulseDistance());
                        lock (Lock)
                        {
                            positions[pos] = newPos;
                        }
                    }
                    SamplePositions[index] = newPos;
                }));
            }
            else
            {
                Vector3 newPos;
                if (positions.ContainsKey(pos))
                {
                    newPos = positions[pos];
                }
                else
                {
                    newPos = Tree.BestPlacePosition(pos, MaxRepulseDistance());
                    positions[pos] = newPos;
                }
                SamplePositions[index] = newPos;
            }
        }

        public void BestPositionOfChildNode(float3 pos, IrradianceVolumeOctreeNode node, int index,
            Dictionary<Vector3, Vector3> positions)
        {
            if (USE_THREAD_POOL)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(obj =>
                {
                    Vector3 newPos;
                    if (positions.ContainsKey(pos))
                    {
                        newPos = positions[pos];
                    }
                    else
                    {
                        newPos = Tree.BestPlacePosition(pos, MaxRepulseDistance());
                        lock (Lock)
                        {
                            positions[pos] = newPos;
                        }
                    }
                    
                    node.SamplePositions[index] = newPos;
                }));
            }
            else
            {
                Vector3 newPos;
                if (positions.ContainsKey(pos))
                {
                    newPos = positions[pos];
                }
                else
                {
                    newPos = Tree.BestPlacePosition(pos, MaxRepulseDistance());
                    positions[pos] = newPos;
                }
                node.SamplePositions[index] = newPos;
            }
        }

        public Vector3[] GetAllDebugPositions(int level)
        {
            HashSet<Vector3> positions = new HashSet<Vector3>();
            if (SamplePositions != null)
            {
                foreach (var pos in SamplePositions)
                {
                    positions.Add(pos);
                }
                if (Nodes != null)
                {
                    foreach (var node in Nodes)
                    {
                        node.GetDebugPositions(level, positions);
                    }
                }
            }

            return positions.ToArray();
        }
    }
}