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
        public static void ExtractToGLTF(string inputMeshWorldPath, string outputGltfPath, string customTextureDir, bool useHierarchy)
        {
            // Declare everything at the top so the whole method can see it
            List<Vertex> verts = new List<Vertex>();
            List<mesh> meshes = new List<mesh>();
            List<RefPoint> refPoints = new List<RefPoint>();
            List<spline> splines = new List<spline>();
            List<LightObj> lights = new List<LightObj>();

            using (FileStream fileStream = File.OpenRead(inputMeshWorldPath))
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                // 1. READ META DATA AT THE VERY BEGINNING!
                refPoints = ReadRefPoints(reader);
                splines = ReadSplines(reader);
                lights = ReadLights(reader);

                // 2. Skip Background/Ambient Colors
                reader.ReadBytes(24); // 6 floats

                // 3. Read Vertices
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

                // 4. Read Root Bounding Cube
                reader.ReadBytes(24);

                // 5. Read Top-Level Meshes
                int topLevelMeshCount = reader.ReadInt32();
                for (int m = 0; m < topLevelMeshCount; m++)
                {
                    meshes.Add(ReadMeshNode(reader));
                }
            } // End of BinaryReader

            // --- BUILD THE GLTF HIERARCHY ---
            var sceneBuilder = new SceneBuilder();

            // Create a Master Collection to hold everything
            var rootNode = new NodeBuilder("Level_Root");

            // Kick off the recursive tree builder!
            foreach (var m in meshes)
            {
                BuildGLTFNode(m, rootNode, sceneBuilder, verts, inputMeshWorldPath, customTextureDir, useHierarchy);
            }

            // Convert the SceneBuilder into the final glTF Document
            var model = sceneBuilder.ToGltf2();
            var scene = model.DefaultScene;

            // --- INJECT METADATA DIRECTLY INTO THE GLTF DOCUMENT ---

            // 1. Inject Ref Points
            // If useHierarchy is true, create a folder at the Root of the scene. Otherwise, no folder.
            var refFolder = useHierarchy ? scene.CreateNode("RefPoints") : null;
            foreach (var rp in refPoints)
            {
                // Attach to the folder if it exists, otherwise attach directly to the scene
                var rpNode = refFolder != null ? refFolder.CreateNode(rp.name) : scene.CreateNode(rp.name);

                // Changed the diagonal 1s to 0.2f to scale down the Empty nodes
                rpNode.LocalTransform = new System.Numerics.Matrix4x4(
                    0.2f, 0, 0, 0,
                    0, 0.2f, 0, 0,
                    0, 0, 0.2f, 0,
                    rp.position.X, rp.position.Y, rp.position.Z, 1
                );
            }

            // 2. Inject Splines
            var splineFolder = useHierarchy ? scene.CreateNode("Splines") : null;
            foreach (var s in splines)
            {
                var sNode = splineFolder != null ? splineFolder.CreateNode(s.name) : scene.CreateNode(s.name);

                int ptIdx = 1;
                foreach (var pt in s.points)
                {
                    var realPt = pt.Unconverted();

                    var ptNode = sNode.CreateNode($"{ptIdx:D2}");

                    // Scaled the points down to 20% here as well
                    ptNode.LocalTransform = new System.Numerics.Matrix4x4(
                        0.2f, 0, 0, 0,
                        0, 0.2f, 0, 0,
                        0, 0, 0.2f, 0,
                        realPt.X, realPt.Y, realPt.Z, 1
                    );
                    ptIdx++;
                }
            }

            // 3. Inject Lights
            var lightsFolder = useHierarchy ? scene.CreateNode("Lights") : null;
            int lIdx = 0;
            foreach (var l in lights)
            {
                string lName = $"Direct_{lIdx++:D2}";
                var lNode = lightsFolder != null ? lightsFolder.CreateNode(lName) : scene.CreateNode(lName);

                // --- THE TARGETING MATH ---
                System.Numerics.Vector3 up = System.Numerics.Vector3.UnitY;
                System.Numerics.Vector3 forward = System.Numerics.Vector3.Normalize(l.direction - l.position);

                if (forward.LengthSquared() < 0.001f) forward = -System.Numerics.Vector3.UnitZ;
                if (System.Math.Abs(System.Numerics.Vector3.Dot(forward, up)) > 0.999f) up = System.Numerics.Vector3.UnitX;

                var viewMatrix = System.Numerics.Matrix4x4.CreateLookAt(l.position, l.direction, up);
                System.Numerics.Matrix4x4.Invert(viewMatrix, out var lightTransform);

                lNode.LocalTransform = lightTransform;

                // --- CREATE THE REAL GLTF LIGHT ---
                try
                {
                    // This is the direct Schema2 approach to add KHR_lights_punctual
                    var punctualLight = model.CreatePunctualLight(SharpGLTF.Schema2.PunctualLightType.Directional);
                    punctualLight.Color = new System.Numerics.Vector3(l.color.X, l.color.Y, l.color.Z);
                    lNode.PunctualLight = punctualLight;
                }
                catch
                {
                    // Failsafe
                    Console.WriteLine($"[WARNING] Could not attach physical light to {lName}. Defaulting to Empty Node.");
                }
            }

            // Save the final, fully populated file!
            model.SaveGLB(outputGltfPath);
        }

        private static List<RefPoint> ReadRefPoints(BinaryReader reader)
        {
            List<RefPoint> points = new List<RefPoint>();
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                RefPoint rp = new RefPoint();

                // Glue the "REF:" prefix back on
                rp.name = "REF:" + ReadGameString(reader);

                rp.position = new System.Numerics.Vector3(
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / -50.0f
                );

                rp.rotation = new System.Numerics.Vector3(
                    reader.ReadSingle(), // RotZ
                    reader.ReadSingle(), // RotY
                    reader.ReadSingle()  // RotX
                );

                int hasColor = reader.ReadInt32();
                if (hasColor == 1)
                {
                    rp.properties = new geom();
                    rp.properties.ambient = ReadVector4(reader);
                    rp.properties.diffuse = ReadVector4(reader);
                    rp.properties.specular = ReadVector4(reader);
                    rp.properties.emissive = ReadVector4(reader);
                    rp.properties.power = reader.ReadSingle();
                    rp.properties.hasReflection = reader.ReadInt32();

                    int hasImage = reader.ReadInt32();
                    if (hasImage == 1)
                    {
                        rp.properties.texture = ReadGameString(reader);
                    }
                }
                points.Add(rp);
            }
            return points;
        }
        private static List<spline> ReadSplines(BinaryReader reader)
        {
            List<spline> splinesList = new List<spline>();
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                spline s = new spline();

                // Glue the "C:" prefix back on
                s.name = "C:" + ReadGameString(reader);

                s.points = new List<Vertex>();

                int ptCount = reader.ReadInt32();
                for (int p = 0; p < ptCount; p++)
                {
                    s.points.Add(new Vertex
                    {
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle()
                    });
                }
                splinesList.Add(s);
            }
            return splinesList;
        }

        private static List<LightObj> ReadLights(BinaryReader reader)
        {
            List<LightObj> lights = new List<LightObj>();
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                LightObj l = new LightObj();
                l.type = reader.ReadInt32();

                // Undo the .Converted() scaling!
                l.position = new System.Numerics.Vector3(
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / -50.0f
                );

                l.direction = new System.Numerics.Vector3(
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / 50.0f,
                    reader.ReadSingle() / -50.0f
                );

                l.color = new System.Numerics.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                lights.Add(l);
            }
            return lights;
        }

        private static void BuildGLTFNode(mesh m, NodeBuilder parentNode, SceneBuilder sceneBuilder, List<Vertex> verts, string inputMeshWorldPath, string customTextureDir, bool useHierarchy)
        {
            NodeBuilder currentNode = parentNode;

            // Only create a new folder node if the checkbox is checked!
            if (useHierarchy)
            {
                currentNode = parentNode.CreateNode(m.name);
            }

            if (m.geoms.Count > 0)
            {
                var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(m.name);

                foreach (var g in m.geoms)
                {
                    var material = new MaterialBuilder(g.texture ?? "Default").WithBaseColor(g.diffuse);

                    // --- TEXTURE LOADING LOGIC ---
                    if (!string.IsNullOrEmpty(g.texture))
                    {
                        string finalTexturePath = "";
                        if (!string.IsNullOrEmpty(customTextureDir))
                        {
                            finalTexturePath = Path.Combine(customTextureDir, g.texture);
                        }
                        else
                        {
                            string meshWorldDir = Path.GetDirectoryName(inputMeshWorldPath);
                            string parentDir = Directory.GetParent(meshWorldDir)?.FullName ?? meshWorldDir;
                            finalTexturePath = Path.Combine(parentDir, "Textures", g.texture);

                            if (!File.Exists(finalTexturePath))
                                finalTexturePath = Path.Combine(meshWorldDir, "textures", g.texture);
                        }

                        if (File.Exists(finalTexturePath))
                        {
                            string ext = Path.GetExtension(finalTexturePath).ToLower();
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                            {
                                material.WithBaseColor(finalTexturePath);
                            }
                            else
                            {
                                try
                                {
                                    using (var bitmap = new System.Drawing.Bitmap(finalTexturePath))
                                    using (var ms = new MemoryStream())
                                    {
                                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        var memImage = new SharpGLTF.Memory.MemoryImage(ms.ToArray());
                                        material.WithBaseColor(memImage);
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    // Add polygons
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
                                new VertexPositionNormal(vertA.X, vertA.Y, vertA.Z, vertA.NX, vertA.NY, vertA.NZ), new VertexTexture1(new Vector2(vertA.U, vertA.V)));
                            var pB = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(vertB.X, vertB.Y, vertB.Z, vertB.NX, vertB.NY, vertB.NZ), new VertexTexture1(new Vector2(vertB.U, vertB.V)));
                            var pC = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                new VertexPositionNormal(vertC.X, vertC.Y, vertC.Z, vertC.NX, vertC.NY, vertC.NZ), new VertexTexture1(new Vector2(vertC.U, vertC.V)));

                            primitive.AddTriangle(pA, pB, pC);
                        }
                    }
                }

                // Lock the mesh into the scene based on the checkbox
                if (useHierarchy)
                {
                    sceneBuilder.AddRigidMesh(meshBuilder, currentNode);
                }
                else
                {
                    // If false, just dump it directly into the center of the world with no parent
                    sceneBuilder.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                }
            }

            // RECURSION!
            foreach (var child in m.children)
            {
                BuildGLTFNode(child, currentNode, sceneBuilder, verts, inputMeshWorldPath, customTextureDir, useHierarchy);
            }
        }

        private static mesh ReadMeshNode(BinaryReader reader)
        {
            reader.ReadBytes(24);

            int childCount = reader.ReadInt32();
            int geomCount = 0;

            if (childCount == 0)
            {
                geomCount = reader.ReadInt32();
            }

            mesh currentMesh = new mesh { geoms = new List<geom>(), children = new List<mesh>() };

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

            if (string.IsNullOrEmpty(currentMesh.name)) currentMesh.name = "Folder";

            // Attach the children directly to this mesh!
            for (int c = 0; c < childCount; c++)
            {
                currentMesh.children.Add(ReadMeshNode(reader));
            }

            return currentMesh;
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

                // Weird winding order
                if (i % 2 == 0)
                {
                    triangles.Add((vA, vC, vB));
                }
                else
                {
                    triangles.Add((vA, vB, vC));
                }
            }
            return triangles;
        }
    }
}