using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace IrradianceVolume
{
    [ExecuteInEditMode]
    public class IrradianceVolumeManager : MonoBehaviour
    {
        private IrradianceVolumeManager instance;

        [Range(5.0f, 100.0f)] public float CellSize = 50.0f;

        public IrradianceVolumeOctree Octree;

        public bool DrawOctreeBounds = false;
        [Range(0, 100)] public int DrawOctreeNode = 0;

        public bool DrawSamplePos = false;
        [Range(0, 5)] public int DrawSamplePosLevel = 0;

        private Dictionary<Vector3, int> positionsID;
        public static IrradianceVolumeManager Instance { get; private set; }

        private void Update()
        {
            Instance = this;
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            Instance = null;
        }

        void GetCompressedSH(SphericalHarmonicsL2 value, out float3 l0, out float3 l1)
        {
            l0 = new float3(value[0, 0], value[1, 0], value[2, 0]);
            float basex = Mathf.Max(value[0, 0], 1e-5f);
            float basey = Mathf.Max(value[1, 0], 1e-5f);
            float basez = Mathf.Max(value[2, 0], 1e-5f);
            l1 = new float3(value[0, 3] / basex + value[1, 3] / basey + value[2, 3] / basez,
                value[0, 1] / basex + value[1, 1] / basey + value[2, 1] / basez,
                value[0, 2] / basex + value[1, 2] / basey + value[2, 2] / basez);
            l1 /= 3.0f;
        }


        public void PlaceDataAt<T>(int3 size, int3 pos, ref NativeArray<T> array, T data) where T : struct
        {
            int index = BoxUtils.IndexToId(size, pos);
            array[index] = data;
        }

        public int3 AdditionalBrickCoord(int3 AllSize, int3 MainBlockSize, int id)
        {
            int3 delta = AllSize - MainBlockSize;

            int3 block1 = new int3(MainBlockSize.x, MainBlockSize.y, delta.z);
            int blocklsize = block1.x * block1.y * block1.z;
            if (id < blocklsize)
            {
                return new int3(0, 0, MainBlockSize.z) + BoxUtils.IdToIndex(block1, id);
            }
            else
            {
                id -= blocklsize;
            }

            int3 block2 = new int3(MainBlockSize.x, delta.y, AllSize.z);
            int block2size = block2.x * block2.y * block2.z;
            if (id < block2size)
            {
                return new int3(0, MainBlockSize.y, 0) + BoxUtils.IdToIndex(block2, id);
            }
            else
            {
                id -= block2size;
            }

            int3 block3 = new int3(delta.x, AllSize.y, AllSize.z);
            return new int3(MainBlockSize.x, 0, 0) + BoxUtils.IdToIndex(block3, id);
        }

        public int3 GetExpandSize(int3 originSize, int extraNumber, int limit = 64)
        {
            if (originSize.x > limit || originSize.y > limit || originSize.z > limit)
            {
                throw new Exception("Too Large");
            }

            int originNumber = originSize.x * originSize.y * originSize.z;
            if (limit * originSize.y * originSize.z >= originNumber + extraNumber)
            {
                return new int3(limit, originSize.y, originSize.z);
            }
            else if (limit * limit * originSize.z >= originNumber + extraNumber)
            {
                return new int3(limit, limit, originSize.z);
            }
            else if (limit * limit * limit >= originNumber + extraNumber)
            {
                return new int3(limit, limit, limit);
            }
            else
            {
                throw new Exception("Exceed");
            }
        }

#if UNITY_EDITOR
        public void DirectSaveData(ref NativeArray<float4> data1, ref NativeArray<float4> data2,
            List<IrradianceVolumeOctreeNode> AdditionalBricks,
            int3 AllBricks, int3 MainBlockBricks)
        {
            Lightmapping.Bake();
            for (int i = 0; i <= Octree.CellCount.x; ++i)
            {
                for (int j = 0; j <= Octree.CellCount.y; ++j)
                {
                    for (int k = 0; k <= Octree.CellCount.z; ++k)
                    {
                        var position =
                            Octree.SamplePositions[BoxUtils.IndexToId(Octree.CellCount + 1, new int3(i, j, k))];
                        SphericalHarmonicsL2 sh;
                        LightProbes.GetInterpolatedProbe(position, null, out sh);
                        GetCompressedSH(sh, out float3 l0, out float3 l1);
                        PlaceDataAt(Octree.TextureSize, new int3(i, j, k), ref data1, new float4(l0, 0));
                        PlaceDataAt(Octree.TextureSize, new int3(i, j, k), ref data2, new float4(l1, 0));
                    }
                }
            }

            for (int m = 0; m < AdditionalBricks.Count; ++m)
            {
                var node = AdditionalBricks[m];
                var BrickIndex = AdditionalBrickCoord(AllBricks, MainBlockBricks, m);
                var block = new int3(IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1,
                    IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1, IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1);
                for (int i = 0; i <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++i)
                {
                    for (int j = 0; j <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++j)
                    {
                        for (int k = 0; k <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++k)
                        {
                            var position = node.SamplePositions[BoxUtils.IndexToId(block, new int3(i, j, k))];
                            SphericalHarmonicsL2 sh;
                            LightProbes.GetInterpolatedProbe(position, null, out sh);
                            GetCompressedSH(sh, out float3 l0, out float3 l1);
                            int3 pos = BrickIndex * (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1) + new int3(i, j, k);
                            PlaceDataAt(Octree.TextureSize, pos, ref data1, new float4(l0, 0));
                            PlaceDataAt(Octree.TextureSize, pos, ref data2, new float4(l1, 0));
                        }
                    }
                }
            }
        }

        public void SaveData()
        {
            //MainBlock的Brick个数
            int3 MainBlockBricks = (Octree.CellCount + IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1) /
                                   (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1);

            Queue<IrradianceVolumeOctreeNode> queue = new Queue<IrradianceVolumeOctreeNode>(Octree.Nodes);
            List<IrradianceVolumeOctreeNode> AdditionalBricks = new List<IrradianceVolumeOctreeNode>();
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node.HasChildren)
                {
                    AdditionalBricks.Add(node);
                    if (node.Depth == IrradianceVolumeOctree.MAX_REFINE_LEVEL - 1) continue;
                    foreach (var child in node.Children)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            int3 AllBricks = GetExpandSize(MainBlockBricks, AdditionalBricks.Count);

            int MappingTextureTexelPerCell = BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES,
                IrradianceVolumeOctree.MAX_REFINE_LEVEL - 2);
            Octree.MappingTextureSize = Octree.CellCount * MappingTextureTexelPerCell;
            Texture3D MappingTexture = new Texture3D(Octree.MappingTextureSize.x, Octree.MappingTextureSize.y,
                Octree.MappingTextureSize.z, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            NativeArray<uint> mappingData = new NativeArray<uint>(
                Octree.MappingTextureSize.x * Octree.MappingTextureSize.y * Octree.MappingTextureSize.z,
                Allocator.Temp);
            for (int i = 0; i < mappingData.Length; ++i)
            {
                int3 index = BoxUtils.IdToIndex(Octree.MappingTextureSize, i);
                mappingData[i] = BoxUtils.PackInt4(new int4(index / MappingTextureTexelPerCell, 1));
            }

            // AdditionalBricks中的node顺序，是可以保证深度较大的节点，处于后面
            for (int i = 0; i < AdditionalBricks.Count; ++i)
            {
                var node = AdditionalBricks[i];
                var BrickIndex = AdditionalBrickCoord(AllBricks, MainBlockBricks, i);
                //缩放是相对于MainBlock中的坐标的缩放，达到最大细分级别时，要做一些特殊处理所以设置为0
                if (node.Depth == IrradianceVolumeOctree.MAX_REFINE_LEVEL - 1)
                {
                    int4 LocationAndScale = new int4(BrickIndex * (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1), 0);
                    var indexes = node.IndexesAtMappingTexture();
                    foreach (var index in indexes)
                    {
                        PlaceDataAt<uint>(Octree.MappingTextureSize, index, ref mappingData,
                            BoxUtils.PackInt4(LocationAndScale));
                    }
                }
                else
                {
                    int Scale = BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES,
                        IrradianceVolumeOctree.MAX_REFINE_LEVEL - 1 - node.Depth);
                    var indexes = node.IndexesAtMappingTexture();
                    int PerTexelMappingTexelSize = BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES,
                        IrradianceVolumeOctree.MAX_REFINE_LEVEL - 3);
                    foreach (var index in indexes)
                    {
                        int4 LocationAndScale =
                            new int4(
                                BrickIndex * (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1) +
                                (index - indexes[0]) / PerTexelMappingTexelSize, Scale);
                        PlaceDataAt<uint>(Octree.MappingTextureSize, index, ref mappingData,
                            BoxUtils.PackInt4(LocationAndScale));
                    }
                }
            }

            Octree.TextureSize = AllBricks * (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1);
            Texture3D AmbientTexture = new Texture3D(Octree.TextureSize.x, Octree.TextureSize.y, Octree.TextureSize.z,
                TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Texture3D SHL2Texture = new Texture3D(Octree.TextureSize.x, Octree.TextureSize.y, Octree.TextureSize.z,
                TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            int ArrayCount = Octree.TextureSize.x * Octree.TextureSize.y * Octree.TextureSize.z;
            NativeArray<uint> data1 = new NativeArray<uint>(ArrayCount, Allocator.Temp);
            NativeArray<uint> data2 = new NativeArray<uint>(ArrayCount, Allocator.Temp);

            Lightmapping.Bake();
            var sh = new NativeArray<SphericalHarmonicsL2>(positionsID.Count, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            var validity = new NativeArray<float>(positionsID.Count, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            var bakedProbeOctahedralDepth = new NativeArray<float>(positionsID.Count * 64, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);

            UnityEditor.Experimental.Lightmapping.GetAdditionalBakedProbes(0, sh, validity, bakedProbeOctahedralDepth);

            for (int i = 0; i <= Octree.CellCount.x; ++i)
            {
                for (int j = 0; j <= Octree.CellCount.y; ++j)
                {
                    for (int k = 0; k <= Octree.CellCount.z; ++k)
                    {
                        var position =
                            Octree.SamplePositions[BoxUtils.IndexToId(Octree.CellCount + 1, new int3(i, j, k))];
                        GetCompressedSH(sh[positionsID[position]], out float3 l0, out float3 l1);
                        PlaceDataAt(Octree.TextureSize, new int3(i, j, k), ref data1, BoxUtils.EncodeRGBM(l0));
                        PlaceDataAt(Octree.TextureSize, new int3(i, j, k), ref data2, BoxUtils.EncodeSH2(l1));

                    }
                }
            }

            for (int m = 0; m < AdditionalBricks.Count; ++m)
            {
                var node = AdditionalBricks[m];
                var BrickIndex = AdditionalBrickCoord(AllBricks, MainBlockBricks, m);
                var block = new int3(IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1,
                    IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1, IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1);
                for (int i = 0; i <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++i)
                {
                    for (int j = 0; j <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++j)
                    {
                        for (int k = 0; k <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++k)
                        {
                            var position = node.SamplePositions[BoxUtils.IndexToId(block, new int3(i, j, k))];
                            GetCompressedSH(sh[positionsID[position]], out float3 l0, out float3 l1);
                            int3 pos = BrickIndex * (IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1) + new int3(i, j, k);
                            PlaceDataAt(Octree.TextureSize, pos, ref data1, BoxUtils.EncodeRGBM(l0));
                            PlaceDataAt(Octree.TextureSize, pos, ref data2, BoxUtils.EncodeSH2(l1));
                        }
                    }
                }
            }

            MappingTexture.SetPixelData(mappingData, 0);
            MappingTexture.Apply();
            AmbientTexture.SetPixelData(data1, 0);
            AmbientTexture.Apply();
            SHL2Texture.SetPixelData(data2, 0);
            SHL2Texture.Apply();

            mappingData.Dispose();
            data1.Dispose();
            data2.Dispose();
            sh.Dispose();
            validity.Dispose();
            bakedProbeOctahedralDepth.Dispose();

            var path = SceneManager.GetActiveScene().path;
            path = path.Substring(0, path.Length - 6);
            Debug.Log(path);
            AssetDatabase.CreateAsset(AmbientTexture, path + "/" + "Volume1" + ".asset");
            AssetDatabase.CreateAsset(SHL2Texture, path + "/" + "Volume2" + ".asset");
            AssetDatabase.CreateAsset(MappingTexture, path + "/" + "Mapping" + ".asset");

            Octree.SHTexture1 = AmbientTexture;
            Octree.SHTexture2 = SHL2Texture;
            Octree.MappingTexture = MappingTexture;
            Octree.Saved = true;

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(Octree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif
        public void RecursiveBuildOctree(ref IrradianceVolumeOctreeNode parentNode,
            ref IrradianceVolumeImportantArea[] areas)
        {
            float3 boxSize = parentNode.ChildCellSize * IrradianceVolumeOctree.SUBDIVIDE_SLICES;
            Bounds b = new Bounds(parentNode.OriginPos + boxSize * 0.5f, boxSize);
            bool needRefine = false;
            foreach (var area in areas)
            {
                Bounds c = new Bounds(area.transform.position, area.size);
                if (b.Intersects(c))
                {
                    needRefine = true;
                    break;
                }
            }

            if (Octree.Tree.IntersectBounds(b))
            {
                needRefine = true;
            }

            if (needRefine)
            {
                if (parentNode.Depth == IrradianceVolumeOctree.MAX_REFINE_LEVEL)
                {
                    parentNode.HasChildren = false;
                }
                else
                {
                    parentNode.Children = new IrradianceVolumeOctreeNode[27];
                    parentNode.HasChildren = true;
                    for (int i = 0; i < 3; ++i)
                    {
                        for (int j = 0; j < 3; ++j)
                        {
                            for (int k = 0; k < 3; ++k)
                            {
                                IrradianceVolumeOctreeNode n = new IrradianceVolumeOctreeNode();
                                n.HasChildren = false;
                                n.Index = new int3(i, j, k);
                                n.Depth = parentNode.Depth + 1;
                                n.OriginPos = parentNode.OriginPos + parentNode.ChildCellSize * new int3(i, j, k);
                                n.ChildCellSize = parentNode.ChildCellSize / IrradianceVolumeOctree.SUBDIVIDE_SLICES;
                                n.Parent = parentNode;
                                parentNode.Children[(k * 3 + j) * 3 + i] = n;
                                RecursiveBuildOctree(ref n, ref areas);
                            }
                        }
                    }
                }
            }
        }

        public void RecursiveAddNodePositions(IrradianceVolumeOctreeNode parentNode,
            ref Dictionary<Vector3, Vector3> positions)
        {
            if (parentNode.HasChildren)
            {
                parentNode.SamplePositions = new float3[BoxUtils.IPow(IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1, 3)];
                var boxgrid = new int3(IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1,
                    IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1, IrradianceVolumeOctree.SUBDIVIDE_SLICES + 1);
                for (int i = 0; i <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++i)
                {
                    for (int j = 0; j <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++j)
                    {
                        for (int k = 0; k <= IrradianceVolumeOctree.SUBDIVIDE_SLICES; ++k)
                        {

                            var pos = parentNode.OriginPos + parentNode.ChildCellSize * new int3(i, j, k);

                            Octree.BestPositionOfChildNode(pos, parentNode,
                                BoxUtils.IndexToId(boxgrid, new int3(i, j, k)), positions);
                            if (parentNode.Depth < IrradianceVolumeOctree.MAX_REFINE_LEVEL - 1 &&
                                parentNode.HasChildren)
                            {
                                foreach (var n in parentNode.Children)
                                {
                                    RecursiveAddNodePositions(n, ref positions);
                                }
                            }
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void PlaceProbes()
        {
            var beginTime = DateTime.Now;
            if (Octree == null)
            {
                Octree = ScriptableObject.CreateInstance<IrradianceVolumeOctree>();
                var path = SceneManager.GetActiveScene().path;
                path = path.Substring(0, path.Length - 6);
                Debug.Log(path);
                Directory.CreateDirectory(path);
                AssetDatabase.CreateAsset(Octree, path + "/" + "Octree" + ".asset");
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }

            var renderers = FindObjectsOfType<Renderer>();
            if (renderers.Length == 0) return;
            var lodGroups = FindObjectsOfType<LODGroup>();
            HashSet<Renderer> ExclusiveRenderers = new HashSet<Renderer>();
            foreach (var lodGroup in lodGroups)
            {
                var lods = lodGroup.GetLODs();
                for (int i = 1; i < lods.Length; ++i)
                {
                    foreach (var renderer in lods[i].renderers)
                    {
                        ExclusiveRenderers.Add(renderer);
                    }
                }
            }

            var set = new HashSet<Renderer>(renderers);
            foreach (var r in ExclusiveRenderers)
            {
                set.Remove(r);
            }

            renderers = set.ToArray();
            Debug.Log("Process " + renderers.Length + " Renderers:");

            Stopwatch sw = new Stopwatch();
            sw.Start();
            Octree.Tree = new BVH(renderers);
            sw.Stop();
            Debug.Log("Generate BVH All Cost: " + sw.ElapsedMilliseconds + "MS");

            sw.Reset();
            sw.Start();
            Bounds bound = renderers[0].bounds;
            foreach (var re in renderers)
            {
                bound.Encapsulate(re.bounds);
            }

            Octree.Saved = false;
            bound.extents += Vector3.one * 0.2f;
            Octree.OriginPos = bound.center - bound.extents;
            Octree.CellCount = new int3(
                Mathf.CeilToInt(bound.extents.x * 2 / CellSize), Mathf.CeilToInt(bound.extents.y * 2 / CellSize),
                Mathf.CeilToInt(bound.extents.z * 2 / CellSize)
            );
            Vector3 box = bound.extents * 2;
            Octree.CellSize = new Vector3(box.x / Octree.CellCount.x, box.y / Octree.CellCount.y,
                box.z / Octree.CellCount.z);
            Octree.Nodes = new IrradianceVolumeOctreeNode[Octree.CellCount.x * Octree.CellCount.y * Octree.CellCount.z];
            for (int id = 0; id < Octree.Nodes.Length; ++id)
            {
                int3 index = BoxUtils.IdToIndex(Octree.CellCount, id);
                IrradianceVolumeOctreeNode node = new IrradianceVolumeOctreeNode();
                node.Index = index;
                node.HasChildren = false;
                node.Depth = 1;
                node.OriginPos = Octree.OriginPos + Octree.CellSize * index;
                node.ChildCellSize = Octree.CellSize / IrradianceVolumeOctree.SUBDIVIDE_SLICES;
                Octree.Nodes[id] = node;
            }

            IrradianceVolumeImportantArea[] areas = FindObjectsOfType<IrradianceVolumeImportantArea>();
            for (int id = 0; id < Octree.Nodes.Length; ++id)
            {
                RecursiveBuildOctree(ref Octree.Nodes[id], ref areas);
            }

            sw.Stop();
            Debug.Log("Generate Octree: " + sw.ElapsedMilliseconds + "MS");

            sw.Reset();
            sw.Start();
            Dictionary<Vector3, Vector3> positions = new Dictionary<Vector3, Vector3>();
            var sampleGridSize = Octree.CellCount + 1;
            Octree.SamplePositions = new float3[sampleGridSize.x * sampleGridSize.y * sampleGridSize.z];

            for (int i = 0; i <= Octree.CellCount.x; ++i)
            {
                for (int j = 0; j <= Octree.CellCount.y; ++j)
                {
                    for (int k = 0; k <= Octree.CellCount.z; ++k)
                    {
                        var pos = Octree.OriginPos + Octree.CellSize * new int3(i, j, k);
                        Octree.BestPositionOfRootNode(pos, BoxUtils.IndexToId(sampleGridSize, new int3(i, j, k)),
                            positions);
                    }
                }
            }

            foreach (var node in Octree.Nodes)
            {
                RecursiveAddNodePositions(node, ref positions);
            }

            while (true)
            {
                //figure out what the max worker thread count it
                System.Threading.ThreadPool.GetMaxThreads(out
                    int maxThreads, out int placeHolder);
                System.Threading.ThreadPool.GetAvailableThreads(out int availThreads,
                    out placeHolder);
                if (availThreads == maxThreads) break;
                // Sleep
                System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }

            sw.Stop();
            Debug.Log("Place " + positions.Count + " Probes, Cost: " + sw.ElapsedMilliseconds + " MS");

            List<Vector3> placePositions = new List<Vector3>();
            int l = 0;
            positionsID = new Dictionary<Vector3, int>();
            foreach (var pair in positions)
            {
                if (!positionsID.ContainsKey(pair.Value))
                {
                    placePositions.Add(pair.Value);
                    positionsID[pair.Value] = l;
                    l++;
                }
            }

            UnityEditor.Experimental.Lightmapping.SetAdditionalBakedProbes(0, placePositions.ToArray());

            EditorUtility.SetDirty(Octree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Place Probes All Cost: " + (DateTime.Now - beginTime));
        }

#endif

        public void OnDrawGizmos()
        {
            if (DrawOctreeBounds && Octree.Tree != null)
            {
                Octree.Tree.DrawGizmos(DrawOctreeNode);
            }
        }

    }



#if UNITY_EDITOR

    [CustomEditor(typeof(IrradianceVolumeManager))]
    public class IrradianceVolumeEditor : Editor
    {
        private IrradianceVolumeManager m_VolumeManager;

        void OnEnable()
        {
            m_VolumeManager = target as IrradianceVolumeManager;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Place Probes"))
            {
                m_VolumeManager.PlaceProbes();
                serializedObject.ApplyModifiedProperties();
            }

            if (GUILayout.Button("Bake Lighting & Save Asset"))
            {
                var bakeBeginTime = DateTime.Now;
                m_VolumeManager.SaveData();
                serializedObject.ApplyModifiedProperties();
                Debug.Log("Bake Lighting & Save Cost " + (DateTime.Now - bakeBeginTime));
            }

            if (GUILayout.Button("Setting All Objects to Light probe"))
            {
                var renderers = GameObject.FindObjectsOfType<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    renderer.receiveGI = ReceiveGI.LightProbes;
                }
            }
        }
    }
#endif


}
