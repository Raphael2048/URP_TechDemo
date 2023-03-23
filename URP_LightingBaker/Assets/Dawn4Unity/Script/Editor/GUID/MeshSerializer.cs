using System;
using System.IO;
using UnityEngine;

namespace GPUBaking.Editor
{
    public class MeshSerializer
    {
        static void WriteVector3Array16Bit(Vector3[] arr, BinaryWriter buf)
        {
            if (arr.Length == 0)
                return;

            // calculate bounding box of the array
            var bounds = new Bounds(arr[0], new Vector3(0.001f, 0.001f, 0.001f));
            foreach (var v in arr)
                bounds.Encapsulate(v);

            // write bounds to stream
            var bmin = bounds.min;
            var bmax = bounds.max;
            buf.Write(bmin.x);
            buf.Write(bmax.x);
            buf.Write(bmin.y);
            buf.Write(bmax.y);
            buf.Write(bmin.z);
            buf.Write(bmax.z);

            // encode vectors as 16 bit integer components between the bounds
            foreach (var v in arr)
            {
                var xx = Mathf.Clamp((v.x - bmin.x) / (bmax.x - bmin.x) * 65535.0f, 0.0f, 65535.0f);
                var yy = Mathf.Clamp((v.y - bmin.y) / (bmax.y - bmin.y) * 65535.0f, 0.0f, 65535.0f);
                var zz = Mathf.Clamp((v.z - bmin.z) / (bmax.z - bmin.z) * 65535.0f, 0.0f, 65535.0f);
                var ix = (ushort)xx;
                var iy = (ushort)yy;
                var iz = (ushort)zz;
                buf.Write(ix);
                buf.Write(iy);
                buf.Write(iz);
            }
        }
        static void WriteVector2Array16Bit(Vector2[] arr, BinaryWriter buf)
        {
            if (arr.Length == 0)
                return;

            // Calculate bounding box of the array
            Vector2 bmin = arr[0] - new Vector2(0.001f, 0.001f);
            Vector2 bmax = arr[0] + new Vector2(0.001f, 0.001f);
            foreach (var v in arr)
            {
                bmin.x = Mathf.Min(bmin.x, v.x);
                bmin.y = Mathf.Min(bmin.y, v.y);
                bmax.x = Mathf.Max(bmax.x, v.x);
                bmax.y = Mathf.Max(bmax.y, v.y);
            }

            // Write bounds to stream
            buf.Write(bmin.x);
            buf.Write(bmax.x);
            buf.Write(bmin.y);
            buf.Write(bmax.y);

            // Encode vectors as 16 bit integer components between the bounds
            foreach (var v in arr)
            {
                var xx = (v.x - bmin.x) / (bmax.x - bmin.x) * 65535.0f;
                var yy = (v.y - bmin.y) / (bmax.y - bmin.y) * 65535.0f;
                var ix = (ushort)xx;
                var iy = (ushort)yy;
                buf.Write(ix);
                buf.Write(iy);
            }
        }

        static void WriteVector3ArrayBytes(Vector3[] arr, BinaryWriter buf)
        {
            // encode vectors as 8 bit integers components in -1.0f .. 1.0f range
            foreach (var v in arr)
            {
                var ix = (byte)Mathf.Clamp(v.x * 127.0f + 128.0f, 0.0f, 255.0f);
                var iy = (byte)Mathf.Clamp(v.y * 127.0f + 128.0f, 0.0f, 255.0f);
                var iz = (byte)Mathf.Clamp(v.z * 127.0f + 128.0f, 0.0f, 255.0f);
                buf.Write(ix);
                buf.Write(iy);
                buf.Write(iz);
            }
        }
        static void WriteVector4ArrayBytes(Vector4[] arr, BinaryWriter buf)
        {
            // Encode vectors as 8 bit integers components in -1.0f .. 1.0f range
            foreach (var v in arr)
            {
                var ix = (byte)Mathf.Clamp(v.x * 127.0f + 128.0f, 0.0f, 255.0f);
                var iy = (byte)Mathf.Clamp(v.y * 127.0f + 128.0f, 0.0f, 255.0f);
                var iz = (byte)Mathf.Clamp(v.z * 127.0f + 128.0f, 0.0f, 255.0f);
                var iw = (byte)Mathf.Clamp(v.w * 127.0f + 128.0f, 0.0f, 255.0f);
                buf.Write(ix);
                buf.Write(iy);
                buf.Write(iz);
                buf.Write(iw);
            }
        }

        // Writes mesh to an array of bytes.
        public static byte[] WriteMesh(Mesh mesh,int LODIndex, bool saveTangents)
        {
            if (!mesh)
                throw new Exception("No mesh given!");

            var verts = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var subMeshCount = mesh.subMeshCount;
            var vertexCount = mesh.vertexCount;
            var uvs = mesh.uv;
            var uvs2 = mesh.uv2;
            var tris = mesh.triangles;

            // figure out vertex format
            byte format = 1;
            if (normals.Length > 0)
                format |= 2;
            if (saveTangents && tangents.Length > 0)
                format |= 4;
            if (uvs.Length > 0)
                format |= 8;
            if (uvs2.Length > 0)
                format |= 16;

            var stream = new MemoryStream();
            var buf = new BinaryWriter(stream);

            // write header
            var vertCount = (ushort)verts.Length;
            var triCount = (ushort)(tris.Length / 3);
            buf.Write(vertCount);
            buf.Write(triCount);
            buf.Write(format);
            // vertex components
            WriteVector3Array16Bit(verts, buf);
            WriteVector3ArrayBytes(normals, buf);
            if (saveTangents)
                WriteVector4ArrayBytes(tangents, buf);
            WriteVector2Array16Bit(uvs, buf);
            WriteVector2Array16Bit(uvs2, buf);
            // triangle indices
            foreach (var idx in tris)
            {
                var idx16 = (ushort)idx;
                buf.Write(idx16);
            }
            buf.Write(LODIndex);
            buf.Write(subMeshCount);
            buf.Write(vertexCount);
            buf.Close();

            return stream.ToArray();
        }
    }
}
