using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CraftMeshExporter
{
    internal static class BinaryStlWriter
    {
        public static void Write(string file, List<Triangle> triangles)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Open(file, FileMode.Create)))
            {
                byte[] header = new byte[80];
                byte[] textBytes = System.Text.Encoding.ASCII.GetBytes("KSP CraftMeshExporter printable STL");
                Array.Copy(textBytes, header, Math.Min(textBytes.Length, header.Length));

                bw.Write(header);
                bw.Write((uint)triangles.Count);

                foreach (Triangle t in triangles)
                {
                    Vector3 n = Vector3.Cross(t.B - t.A, t.C - t.A).normalized;

                    WriteVector(bw, n);
                    WriteVector(bw, t.A);
                    WriteVector(bw, t.B);
                    WriteVector(bw, t.C);
                    bw.Write((ushort)0);
                }
            }

            Debug.Log("[CraftMeshExporter] Wrote STL: " + file);
        }

        private static void WriteVector(BinaryWriter bw, Vector3 v)
        {
            bw.Write(v.x);
            bw.Write(v.y);
            bw.Write(v.z);
        }
    }
}
