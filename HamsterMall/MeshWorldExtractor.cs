using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace HamsterMall
{
    public class MeshWorldExtractor
    {
        // Known folder node names that must NOT be treated as ref points
        private static readonly HashSet<string> _folderNames = new HashSet<string>
        {
            "RefPoints", "Splines", "Lights", "Level_Root"
        };

        // Collected during BuildGLTFNode, applied to Schema2.Material after ToGltf2()
        private class MaterialExtras
        {
            public string materialName;
            public Vector4 ambient;
            public Vector4 specular;
            public float power;
            public int hasReflection;
        }

        public static void ExtractToGLTF(string inputMeshWorldPath, string outputGltfPath, string customTextureDir, bool useHierarchy, bool thorough, bool embedTextures)
        {
            List<Vertex> verts = new List<Vertex>();
            List<mesh> meshes = new List<mesh>();
            List<RefPoint> refPoints = new List<RefPoint>();
            List<spline> splines = new List<spline>();
            List<LightObj> lights = new List<LightObj>();

            // Scene-level metadata
            Vector3 backgroundColor = Vector3.Zero;
            Vector3 ambientColor = Vector3.Zero;
            Vector3 rootBoundMin = Vector3.Zero;
            Vector3 rootBoundMax = Vector3.Zero;

            // Material extras to apply after ToGltf2() — always stored so Blender Custom Properties have them
            List<MaterialExtras> pendingMaterialExtras = new List<MaterialExtras>();

            using (FileStream fileStream = File.OpenRead(inputMeshWorldPath))
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                refPoints = ReadRefPoints(reader);
                splines = ReadSplines(reader);
                lights = ReadLights(reader);

                // 2. Read Background/Ambient Colors (6 floats = 24 bytes)
                backgroundColor = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                ambientColor = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

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

                // 4. Read Root Bounding Cube (6 floats = 24 bytes)
                rootBoundMin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                rootBoundMax = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                // 5. Read Top-Level Meshes
                int topLevelMeshCount = reader.ReadInt32();
                for (int m = 0; m < topLevelMeshCount; m++)
                {
                    meshes.Add(ReadMeshNode(reader));
                }
            }

            // --- BUILD THE GLTF HIERARCHY (Toolkit layer) ---

            var sceneBuilder = new SceneBuilder();
            var rootNode = new NodeBuilder("Level_Root");
            var usedMaterialNames = new HashSet<string>();

            foreach (var m in meshes)
            {
                BuildGLTFNode(m, rootNode, sceneBuilder, verts, inputMeshWorldPath, customTextureDir, useHierarchy, thorough, pendingMaterialExtras, usedMaterialNames);
            }

            var model = sceneBuilder.ToGltf2();
            var scene = model.DefaultScene;

            // --- SET IMAGE NAMES ---
            // SharpGLTF doesn't always propagate image names from MaterialBuilder,
            // especially for GLB-embedded textures created from byte arrays.
            // Set them explicitly here so the exporter can recover the original
            // texture filename. The material name was set to the texture name
            // (without extension) during BuildGLTFNode.
            foreach (var mat in model.LogicalMaterials)
            {
                var baseColorTex = mat.FindChannel("BaseColor")?.Texture;
                if (baseColorTex != null)
                {
                    var img = baseColorTex.PrimaryImage;
                    if (img != null && string.IsNullOrEmpty(img.Name))
                    {
                        // Material name is the texture name without extension
                        img.Name = Path.GetFileNameWithoutExtension(mat.Name);
                    }
                }
            }

            // --- APPLY MATERIAL EXTRAS ---
            // Thorough mode: writes all four fields (ambient, specular, power, hasReflection).
            // Non-thorough mode: writes only ambient and hasReflection (specular/power use PBR sliders).
            if (pendingMaterialExtras != null)
            {
                foreach (var matEx in pendingMaterialExtras)
                {
                    var mat = model.LogicalMaterials.FirstOrDefault(m => m.Name == matEx.materialName);
                    if (mat != null)
                    {
                        var dict = mat.TryUseExtrasAsDictionary(true);
                        dict["ambient"] = BuildVec4Array(matEx.ambient);
                        dict["hasReflection"] = matEx.hasReflection;

                        // Only store specular/power in thorough mode — otherwise let the exporter
                        // derive them from Blender's metallic/roughness sliders
                        if (thorough)
                        {
                            dict["specular"] = BuildVec4Array(matEx.specular);
                            dict["power"] = matEx.power;
                        }
                    }
                }
            }

            // --- STORE SCENE-LEVEL METADATA ON A DUMMY NODE (so Blender can see it as Custom Properties) ---

            var metaNode = scene.CreateNode("SceneMetadata");
            var metaExtras = metaNode.TryUseExtrasAsDictionary(true);
            metaExtras["backgroundColor"] = BuildVec3Array(backgroundColor);
            metaExtras["ambientColor"] = BuildVec3Array(ambientColor);
            metaExtras["rootBoundMin"] = BuildVec3Array(rootBoundMin);
            metaExtras["rootBoundMax"] = BuildVec3Array(rootBoundMax);

            // 1. Inject Ref Points (Schema2.Node — has TryUseExtrasAsDictionary ✓)
            var refFolder = useHierarchy ? scene.CreateNode("RefPoints") : null;
            foreach (var rp in refPoints)
            {
                var rpNode = refFolder != null ? refFolder.CreateNode(rp.name) : scene.CreateNode(rp.name);

                // Apply BOTH position and rotation from the ref point.
                // MESHWORLD stores rotation as Euler degrees in (RotZ, RotY, RotX) order.
                float rotRadZ = rp.rotation.X * ((float)Math.PI / 180.0f);
                float rotRadY = rp.rotation.Y * ((float)Math.PI / 180.0f);
                float rotRadX = rp.rotation.Z * ((float)Math.PI / 180.0f);
                Quaternion quat = Quaternion.CreateFromYawPitchRoll(rotRadY, rotRadX, rotRadZ);

                rpNode.LocalTransform = new AffineTransform(
                    new Vector3(0.2f, 0.2f, 0.2f),
                    quat,
                    rp.position
                );

                // Store ref point material properties as node extras.
                // If the original MESHWORLD file had a color block, store all the
                // material fields. If it did NOT, store hasColor=0 explicitly so the
                // exporter knows this ref point had no material (round-trip fidelity —
                // otherwise a Blender-created ref point and an extracted-but-uncolored
                // ref point would be indistinguishable).
                var rpExtras = rpNode.TryUseExtrasAsDictionary(true);
                if (rp.hasColorBlock)
                {
                    rpExtras["hasColor"] = 1;
                    rpExtras["ambient"] = BuildVec4Array(rp.properties.ambient);
                    rpExtras["diffuse"] = BuildVec4Array(rp.properties.diffuse);
                    rpExtras["specular"] = BuildVec4Array(rp.properties.specular);
                    rpExtras["emissive"] = BuildVec4Array(rp.properties.emissive);
                    rpExtras["power"] = rp.properties.power;
                    rpExtras["hasReflection"] = rp.properties.hasReflection;
                    rpExtras["texture"] = rp.properties.texture ?? "";
                }
                else
                {
                    rpExtras["hasColor"] = 0;
                }
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

                    ptNode.LocalTransform = new System.Numerics.Matrix4x4(
                        0.2f, 0, 0, 0,
                        0, 0.2f, 0, 0,
                        0, 0, 0.2f, 0,
                        realPt.X, realPt.Y, realPt.Z, 1
                    );
                    ptIdx++;
                }
            }

            // 3. Inject Lights — as real glTF PunctualLights
            // MESHWORLD lights are all directional. We create a single node per light
            // with a PunctualLight (Directional) and set its rotation to point in the
            // direction stored in the MESHWORLD file.
            var lightsFolder = useHierarchy ? scene.CreateNode("Lights") : null;
            int lIdx = 0;
            foreach (var l in lights)
            {
                string lightName = $"Light_{lIdx:D2}";
                lIdx++;

                var lNode = lightsFolder != null ? lightsFolder.CreateNode(lightName) : scene.CreateNode(lightName);

                // Position
                lNode.LocalTransform = new AffineTransform(
                    new Vector3(0.2f),
                    Quaternion.Identity,
                    l.position
                );

                // Calculate rotation so the light points from position toward direction.
                // MESHWORLD stores direction as a point in space; the light direction is
                // (direction - position) normalized.
                Vector3 dir = Vector3.Normalize(l.direction - l.position);
                // glTF lights point down -Z by default. Build a rotation that maps -Z to dir.
                Vector3 forward = new Vector3(0, 0, -1);
                if (dir != Vector3.Zero && dir != forward)
                {
                    float dot = Vector3.Dot(forward, dir);
                    if (dot > 0.9999f)
                    {
                        // Already pointing forward
                    }
                    else if (dot < -0.9999f)
                    {
                        // Pointing backward — rotate 180° around Y
                        lNode.LocalTransform = new AffineTransform(
                            new Vector3(0.2f),
                            Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), (float)Math.PI),
                            l.position
                        );
                    }
                    else
                    {
                        Vector3 axis = Vector3.Normalize(Vector3.Cross(forward, dir));
                        float angle = (float)Math.Acos(dot);
                        Quaternion quat = Quaternion.CreateFromAxisAngle(axis, angle);
                        lNode.LocalTransform = new AffineTransform(
                            new Vector3(0.2f),
                            quat,
                            l.position
                        );
                    }
                }

                // Store extras for MESHWORLD round-trip data that PunctualLight can't represent
                var lightExtras = lNode.TryUseExtrasAsDictionary(true);
                lightExtras["type"] = l.type;

                // Attach real glTF PunctualLight so Blender shows an actual light
                try
                {
                    var punctualLight = model.CreatePunctualLight(SharpGLTF.Schema2.PunctualLightType.Directional);
                    punctualLight.Color = new System.Numerics.Vector3(l.color.X, l.color.Y, l.color.Z);
                    lNode.PunctualLight = punctualLight;
                }
                catch
                {
                    Console.WriteLine($"[WARNING] Could not attach PunctualLight to {lightName}.");
                }
            }

            // --- SAVE ---
            if (embedTextures)
            {
                // Save as GLB with textures embedded
                model.SaveGLB(outputGltfPath);
            }
            else
            {
                // Save as GLTF with external texture files.
                // SharpGLTF writes satellite image files using its own naming convention
                // (modelname_0.png, etc.) — the image Name property is preserved inside
                // the GLTF JSON (Blender shows correct names) but the satellite files
                // on disk get the wrong names. The exporter's WriteTextures method handles
                // writing correctly-named files from the GLTF, so this is OK for round-trip.
                string gltfPath = Path.ChangeExtension(outputGltfPath, ".gltf");
                model.SaveGLTF(gltfPath);
            }
        }

        // ─── HELPERS: Build arrays for Vector types (JSON arrays → Blender float arrays) ───

        private static List<object> BuildVec3Array(Vector3 v)
        {
            return new List<object> { v.X, v.Y, v.Z };
        }

        private static List<object> BuildVec4Array(Vector4 v)
        {
            return new List<object> { v.X, v.Y, v.Z, v.W };
        }

        private static List<object> BuildColorArray(Vector3 v)
        {
            return new List<object> { v.X, v.Y, v.Z };
        }

        // ─── BINARY READERS ───

        private static List<RefPoint> ReadRefPoints(BinaryReader reader)
        {
            List<RefPoint> points = new List<RefPoint>();
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                RefPoint rp = new RefPoint();
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
                    rp.hasColorBlock = true;
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

        private static void BuildGLTFNode(mesh m, NodeBuilder parentNode, SceneBuilder sceneBuilder,
            List<Vertex> verts, string inputMeshWorldPath, string customTextureDir, bool useHierarchy,
            bool thorough, List<MaterialExtras> pendingMaterialExtras, HashSet<string> usedMaterialNames)
        {
            NodeBuilder currentNode = parentNode;

            if (useHierarchy)
            {
                currentNode = parentNode.CreateNode(m.name);
            }

            if (m.geoms.Count > 0)
            {
                foreach (var g in m.geoms)
                {
                    var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(g.name);

                    // Name each material uniquely per-geom so SharpGLTF doesn't merge
                    // distinct materials that happen to share a name.
                    //
                    // Background: SharpGLTF's SceneBuilder.ToGltf2() groups materials by
                    // ContentComparer (which includes Name). When many geoms are untextured,
                    // they previously all got the name "Default" and collapsed into ONE
                    // Schema2.Material (the first one in the group) — silently discarding
                    // every other geom's WithMetallicRoughness/extras. That made the
                    // Intermediate rails (36 geoms, all untextured) come back from a
                    // round-trip much lighter/shinier than they should be.
                    //
                    // Unique naming + SharpGLTF's content-based re-merge gives the best of
                    // both worlds: identical materials still collapse to one in the GLB
                    // (batch-editability preserved), while genuinely different materials
                    // keep their values.
                    string matBase = !string.IsNullOrEmpty(g.texture)
                        ? g.texture
                        : (!string.IsNullOrEmpty(g.name) ? g.name : "Default");

                    string matName = matBase;
                    int matSuffix = 2;
                    while (!usedMaterialNames.Add(matName))
                    {
                        matName = matBase + "_" + matSuffix++;
                    }

                    var material = new MaterialBuilder(matName)
                        .WithBaseColor(g.diffuse);

                    // Write emissive to glTF material so exporter can read it back
                    if (g.emissive != Vector4.Zero)
                    {
                        material.WithEmissive(new Vector3(g.emissive.X, g.emissive.Y, g.emissive.Z));
                    }

                    // Store material extras for fields that PBR sliders can't represent.
                    // Non-thorough: store only ambient and hasReflection (specular/power use PBR sliders).
                    // Thorough: store all four (ambient, specular, power, hasReflection).
                    if (thorough)
                    {
                        pendingMaterialExtras.Add(new MaterialExtras
                        {
                            materialName = matName,
                            ambient = g.ambient,
                            specular = g.specular,
                            power = g.power,
                            hasReflection = g.hasReflection
                        });
                    }
                    else
                    {
                        // Non-thorough: only store what PBR can't represent
                        pendingMaterialExtras.Add(new MaterialExtras
                        {
                            materialName = matName,
                            ambient = g.ambient,
                            specular = Vector4.Zero,
                            power = 0,
                            hasReflection = g.hasReflection
                        });

                        // Also map specular→metallic and power→roughness for Blender slider editing
                        float metallic = (g.specular.X + g.specular.Y + g.specular.Z) / 3.0f;
                        float roughness = 1.0f - System.Math.Min(g.power / 100.0f, 1.0f);
                        material.WithMetallicRoughness(metallic, roughness);
                    }

                    // Texture Loading
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

                    if (useHierarchy)
                    {
                        var geomNode = currentNode.CreateNode(g.name);
                        sceneBuilder.AddRigidMesh(meshBuilder, geomNode);
                    }
                    else
                    {
                        var flatNode = parentNode.CreateNode(g.name);
                        sceneBuilder.AddRigidMesh(meshBuilder, flatNode);
                    }
                }
            }

            foreach (var child in m.children)
            {
                BuildGLTFNode(child, currentNode, sceneBuilder, verts, inputMeshWorldPath, customTextureDir, useHierarchy, thorough, pendingMaterialExtras, usedMaterialNames);
            }
        }

        private static mesh ReadMeshNode(BinaryReader reader)
        {
            reader.ReadBytes(24); // bounding box

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
                currentGeom.name = ReadGameString(reader);

                if (g == 0) currentMesh.name = "Chunk_" + currentGeom.name;

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

            for (int c = 0; c < childCount; c++)
            {
                currentMesh.children.Add(ReadMeshNode(reader));
            }

            return currentMesh;
        }

        private static Vector4 ReadVector4(BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static string ReadGameString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length <= 0) return "";

            char[] chars = reader.ReadChars(length - 1);
            reader.ReadByte(); // null terminator
            return new string(chars);
        }

        private static List<(int A, int B, int C)> Unstripify(strip s)
        {
            List<(int A, int B, int C)> triangles = new List<(int, int, int)>();
            for (int i = 0; i < s.triangleCount; i++)
            {
                int vA = s.vertexOffset + i;
                int vB = s.vertexOffset + i + 1;
                int vC = s.vertexOffset + i + 2;

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
