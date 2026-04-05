using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace HamsterMall
{
    public class MeshWorldExtractor
    {
        public static string LastSuccessfulAction = "Starting extraction...";

        public static void ExtractToGLTF(string inputMeshWorldPath, string outputGltfPath)
        {
            List<Vertex> verts = new List<Vertex>();
            List<mesh> meshes = new List<mesh>();

            using (FileStream fileStream = File.OpenRead(inputMeshWorldPath))
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                // 1. Skip Ref Points (We don't need them for the pure 3D model)
                int refCount = reader.ReadInt32();
                for (int i = 0; i < refCount; i++)
                {
                    ReadGameString(reader); // Name
                    reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // Position
                    reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle(); // Rotation

                    int hasColor = reader.ReadInt32();
                    if (hasColor == 1)
                    {
                        reader.ReadBytes(16 * 4); // 4 Vector4s (Ambient, Diffuse, Spec, Emissive)
                        reader.ReadSingle(); // Power
                        reader.ReadInt32(); // HasReflection
                        int hasImage = reader.ReadInt32();
                        if (hasImage == 1) ReadGameString(reader);
                    }
                }

                // 2. Skip Splines
                int splineCount = reader.ReadInt32();
                for (int i = 0; i < splineCount; i++)
                {
                    ReadGameString(reader); // Name
                    int pointCount = reader.ReadInt32();
                    for (int p = 0; p < pointCount; p++)
                    {
                        reader.ReadSingle(); reader.ReadSingle(); reader.ReadSingle();
                    }
                }

                // 3. Skip Lights
                int lightCount = reader.ReadInt32();
                for (int i = 0; i < lightCount; i++)
                {
                    reader.ReadInt32(); // 0 spacer
                    reader.ReadBytes(36); // 9 floats (Pos, Dir, Scale)
                }

                // 4. Skip Background/Ambient Colors
                reader.ReadBytes(24); // 6 floats

                // 5. Read Vertices
                int vertCount = reader.ReadInt32();
                for (int i = 0; i < vertCount; i++)
                {
                    verts.Add(new Vertex
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle(),
                        NX = reader.ReadSingle(),
                        NY = reader.ReadSingle(),
                        NZ = reader.ReadSingle(),
                        U = reader.ReadSingle(),
                        V = reader.ReadSingle()
                    });
                }

                // 6. Read Root Bounding Cube (The Special Case)
                reader.ReadBytes(24);

                // 7. Read Top-Level Meshes
                int topLevelMeshCount = reader.ReadInt32();

                // The Root node has NO geom count! We just jump straight into the meshes.
                for (int m = 0; m < topLevelMeshCount; m++)
                {
                    ReadMeshNode(reader, meshes);
                }
            } // End of BinaryReader

            // --- BUILD THE GLTF ---
            var sceneBuilder = new SceneBuilder();

            foreach (var m in meshes)
            {
                var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(m.name);

                foreach (var g in m.geoms)
                {
                    var material = new MaterialBuilder(g.texture ?? "Default")
                        .WithBaseColor(g.diffuse);

                    var primitive = meshBuilder.UsePrimitive(material);

                    foreach (var s in g.strips)
                    {
                        var triangles = Unstripify(s);
                        foreach (var tri in triangles)
                        {
                            var vertA = verts[tri.A].Unconverted();
                            var vertB = verts[tri.B].Unconverted();
                            var vertC = verts[tri.C].Unconverted();

                            var pA = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(vertA.X, vertA.Y, vertA.Z, vertA.NX, vertA.NY, vertA.NZ),
                                new VertexTexture1(new Vector2(vertA.U, vertA.V)));

                            var pB = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(vertB.X, vertB.Y, vertB.Z, vertB.NX, vertB.NY, vertB.NZ),
                                new VertexTexture1(new Vector2(vertB.U, vertB.V)));

                            var pC = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(vertC.X, vertC.Y, vertC.Z, vertC.NX, vertC.NY, vertC.NZ),
                                new VertexTexture1(new Vector2(vertC.U, vertC.V)));

                            primitive.AddTriangle(pA, pB, pC);
                        }
                    }
                }
                sceneBuilder.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
            }

            var model = sceneBuilder.ToGltf2();
            model.SaveGLB(outputGltfPath);
        }

        private static void ReadMeshNode(BinaryReader reader, List<mesh> allMeshes)
        {
            // 1. Read Bounding Box
            reader.ReadBytes(24);

            // 2. The 4-Byte Secret!
            int childCount = reader.ReadInt32();
            int geomCount = 0;

            // Only "Leaf" nodes (meshes) have a geometry count. Folders skip this byte!
            if (childCount == 0)
            {
                geomCount = reader.ReadInt32();
            }

            mesh currentMesh = new mesh { geoms = new List<geom>() };

            // 3. Parse Geoms (If it's a mesh)
            for (int g = 0; g < geomCount; g++)
            {
                geom currentGeom = new geom { strips = new List<strip>() };

                string nameFromGeom = ReadGameString(reader);
                if (g == 0) currentMesh.name = nameFromGeom;

                currentGeom.ambient = ReadVector4(reader);
                currentGeom.diffuse = ReadVector4(reader);
                currentGeom.specular = ReadVector4(reader);
                currentGeom.emissive = ReadVector4(reader);

                currentGeom.power = reader.ReadSingle();
                currentGeom.hasReflection = reader.ReadInt32();

                int hasTexture = reader.ReadInt32();
                if (hasTexture != 0)
                {
                    currentGeom.texture = ReadGameString(reader);
                }

                int stripCount = reader.ReadInt32();
                for (int s = 0; s < stripCount; s++)
                {
                    currentGeom.strips.Add(new strip { triangleCount = reader.ReadInt32(), vertexOffset = reader.ReadInt32() });
                }
                currentMesh.geoms.Add(currentGeom);
            }

            if (currentMesh.geoms.Count > 0)
            {
                if (string.IsNullOrEmpty(currentMesh.name)) currentMesh.name = "Submesh";
                allMeshes.Add(currentMesh);
            }

            // 4. Parse Children (If it's a folder)
            for (int c = 0; c < childCount; c++)
            {
                ReadMeshNode(reader, allMeshes);
            }
        }

        // Helper to read 4 floats directly into a Vector4
        private static Vector4 ReadVector4(BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static string ReadGameString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length <= 0) return "";

            char[] chars = reader.ReadChars(length - 1);
            reader.ReadByte(); // Consume the null terminator
            return new string(chars);
        }

        // Unstripify Logic
        private static List<(int A, int B, int C)> Unstripify(strip s)
        {
            List<(int A, int B, int C)> triangles = new List<(int, int, int)>();
            for (int i = 0; i < s.triangleCount; i++)
            {
                int vA = s.vertexOffset + i;
                int vB = s.vertexOffset + i + 1;
                int vC = s.vertexOffset + i + 2;

                if (i % 2 == 0) triangles.Add((vA, vB, vC));
                else triangles.Add((vA, vC, vB));
            }
            return triangles;
        }
    }
}