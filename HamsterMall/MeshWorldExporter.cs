using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace HamsterMall
{
    public class MeshWorldExporter
    {
        // Known folder node names that must NOT be treated as ref points
        private static readonly HashSet<string> _folderNames = new HashSet<string>
        {
            "RefPoints", "Splines", "Lights", "Level_Root", "SceneMetadata"
        };

        public static void ExportFromGLTF(string gltfPath, string savePath, Color ambientColor, Color backgroundColor)
        {
            using (FileStream saveFile = File.OpenWrite(savePath))
            {
                using (CustomWriter writer = new CustomWriter(saveFile))
                {
                    var model = ModelRoot.Load(gltfPath);

                    WriteRefPoints(writer, model);
                    WriteSplines(writer, model);
                    WriteLights(writer, model);

                    // Try to read scene-level metadata from the "SceneMetadata" dummy node's extras.
                    // If not found, fall back to the form-provided colors.
                    Vector3 bgColor = new Vector3(backgroundColor.R / 255.0f, backgroundColor.G / 255.0f, backgroundColor.B / 255.0f);
                    Vector3 ambColor = new Vector3(ambientColor.R / 255.0f, ambientColor.G / 255.0f, ambientColor.B / 255.0f);
                    Vector3 rootMin = new Vector3(-1000000.0f, -1000000.0f, -1000000.0f);
                    Vector3 rootMax = new Vector3(1000000.0f, 1000000.0f, 1000000.0f);

                    var metaNode = model.LogicalNodes.FirstOrDefault(n => n.Name == "SceneMetadata");
                    var extrasDict = metaNode?.TryUseExtrasAsDictionary(false);
                    if (extrasDict != null)
                    {
                        if (extrasDict.ContainsKey("backgroundColor"))
                            bgColor = ReadVec3FromExtras(extrasDict, "backgroundColor", bgColor);
                        if (extrasDict.ContainsKey("ambientColor"))
                            ambColor = ReadVec3FromExtras(extrasDict, "ambientColor", ambColor);
                        if (extrasDict.ContainsKey("rootBoundMin"))
                            rootMin = ReadVec3FromExtras(extrasDict, "rootBoundMin", rootMin);
                        if (extrasDict.ContainsKey("rootBoundMax"))
                            rootMax = ReadVec3FromExtras(extrasDict, "rootBoundMax", rootMax);
                    }

                    WriteBackgroundAndAmbient(writer, bgColor, ambColor);

                    WriteVertices(writer, model, rootMin, rootMax);

                    var saveFileInfo = new FileInfo(savePath);
                    var textureDirectoryPath = Path.Combine(saveFileInfo.DirectoryName, "textures");
                    EnsureClearDirectory(textureDirectoryPath);
                    WriteTextures(model, textureDirectoryPath);
                }
            }
        }

        private static void EnsureClearDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            Directory.CreateDirectory(path);
        }

        private static void WriteTextures(ModelRoot model, string textureDirectoryPath)
        {
            // Collect unique textures from mesh primitives.
            var textures = model.LogicalMaterials
                .Select(mat => new
                {
                    Image = mat.FindChannel("BaseColor")?.Texture?.PrimaryImage,
                    MaterialName = mat.Name
                })
                .Where(x => x.Image != null)
                .GroupBy(x => x.Image.Name)
                .Select(g => g.First());

            foreach (var entry in textures)
            {
                string imageName = entry.Image.Name;
                // If image has no name (e.g. embedded in GLB by another tool), fall back to material name
                if (string.IsNullOrEmpty(imageName))
                    imageName = entry.MaterialName ?? "texture";
                // Strip any existing extension so we don't get "name.png.png"
                imageName = Path.GetFileNameWithoutExtension(imageName);
                var pngBytes = entry.Image.Content.Content.ToArray();
                var pngPath = Path.Combine(textureDirectoryPath, imageName + ".png");
                File.WriteAllBytes(pngPath, pngBytes);
            }
        }

        private static void WriteRefPoints(CustomWriter writer, ModelRoot model)
        {
            var Nodes = new List<Node>();
            foreach (Node node in model.LogicalNodes)
            {
                // Exclude known folder nodes — they are structural, not ref points
                if (_folderNames.Contains(node.Name))
                    continue;

                if (!node.Name.StartsWith("C:") && node.PunctualLight == null)
                {
                    if (node.VisualParent == null || !_folderNames.Contains(node.VisualParent.Name))
                    {
                        if (node.VisualParent == null || !node.VisualParent.Name.StartsWith("C:"))
                        {
                            if (node.Mesh == null || node.Name.StartsWith("REF:"))
                            {
                                Nodes.Add(node);
                            }
                        }
                    }
                }
            }

            writer.Write(Nodes.Count);

            foreach (var node in Nodes)
            {
                int startLength = 0;
                bool REF = false;

                if (node.Name.StartsWith("REF:"))
                {
                    startLength = 4;
                    if (node.Name.StartsWith("REF:FLAG") || node.Name.StartsWith("REF:BRIDGE") || node.Name.StartsWith("REF:SMALLFLAG"))
                    {
                        REF = true;
                    }
                }

                // Strip Blender-style ".001" suffixes but preserve original dots in names
                // by using the LAST dot only if there are digits after it
                int length = node.Name.Length;
                int dotIdx = node.Name.LastIndexOf(".");
                if (dotIdx > startLength && dotIdx + 1 < node.Name.Length)
                {
                    // Check if everything after the dot is a number (Blender suffix like .001, .002)
                    string afterDot = node.Name.Substring(dotIdx + 1);
                    bool isNumeric = true;
                    foreach (char c in afterDot)
                    {
                        if (!char.IsDigit(c)) { isNumeric = false; break; }
                    }
                    if (isNumeric)
                    {
                        length = dotIdx;
                    }
                }

                writer.Write(node.Name.Substring(startLength, length - startLength));
                writer.Write(node.WorldMatrix.Translation.X * 50.0f);
                writer.Write(node.WorldMatrix.Translation.Y * 50.0f);
                writer.Write(-node.WorldMatrix.Translation.Z * 50.0f);

                // Read rotation from node's local transform (quaternion → Euler degrees)
                {
                    Quaternion q = node.LocalTransform.Rotation;
                    float rY = q.X;
                    float rX = q.Y;
                    float rZ = -q.Z;
                    float rW = q.W;

                    float RotX = 0;
                    float RotY = 0;
                    float RotZ = 0;

                    if (1 - 2 * (rX * rX + rY * rY) != 0)
                    {
                        RotY = (float)(180 * Math.Atan2(2 * (rW * rX + rY * rZ), (1 - 2 * (rX * rX + rY * rY))) / Math.PI);
                    }

                    if (1 - 2 * (rY * rY + rZ * rZ) != 0)
                    {
                        RotZ = (float)(180 * Math.Atan2(2 * (rW * rZ + rX * rY), (1 - 2 * (rY * rY + rZ * rZ))) / Math.PI);
                    }
                    RotX = (float)(180 * Math.Asin(2 * (rW * rY - rZ * rX)) / Math.PI);

                    if (float.IsNaN(RotY))
                    {
                        if (rW * rY - rZ * rX > 0)
                        {
                            RotY = 90;
                        }
                        else
                        {
                            RotY = -90;
                        }
                    }
                    writer.Write(RotZ); // Rotation Z
                    writer.Write(RotY); // Rotation Y
                    writer.Write(RotX); // Rotation X
                }

                // Check if this ref point has stored material data in its extras
                var nodeExtras = node.TryUseExtrasAsDictionary(false);
                bool hasColor = REF;

                if (nodeExtras != null && nodeExtras.ContainsKey("hasColor"))
                {
                    hasColor = Convert.ToInt32(nodeExtras["hasColor"]) == 1;
                }

                if (hasColor)
                {
                    writer.Write(1); // Has color

                    Vector4 ambient = new Vector4(0.9921f, 0.9921f, 0.9921f, 1f);
                    Vector4 diffuse = new Vector4(0.9921f, 0.9921f, 0.9921f, 1f);
                    Vector4 specular = Vector4.Zero;
                    Vector4 emissive = Vector4.Zero;
                    float power = 10f;
                    int hasReflection = 0;
                    string textureName = null;

                    // Read material properties from node extras (stored by extractor)
                    if (nodeExtras != null)
                    {
                        ambient = ReadVec4FromExtras(nodeExtras, "ambient", ambient);
                        diffuse = ReadVec4FromExtras(nodeExtras, "diffuse", diffuse);
                        specular = ReadVec4FromExtras(nodeExtras, "specular", specular);
                        emissive = ReadVec4FromExtras(nodeExtras, "emissive", emissive);

                        if (nodeExtras.ContainsKey("power"))
                            power = Convert.ToSingle(nodeExtras["power"]);
                        if (nodeExtras.ContainsKey("hasReflection"))
                            hasReflection = Convert.ToInt32(nodeExtras["hasReflection"]);
                        if (nodeExtras.ContainsKey("texture"))
                        {
                            string tex = nodeExtras["texture"] as string;
                            if (!string.IsNullOrEmpty(tex))
                                textureName = tex;
                        }
                    }

                    // For REF flag/bridge/smallflag nodes, also check for attached mesh texture
                    if (REF && textureName == null && node.Mesh != null)
                    {
                        var Primitive = node.Mesh.Primitives;
                        if (Primitive.Count > 0)
                        {
                            var tex = Primitive[0].Material?.FindChannel("BaseColor")?.Texture;
                            if (tex != null)
                            {
                                textureName = tex.PrimaryImage.Name;
                            }
                        }
                    }

                    writer.Write(ambient);
                    writer.Write(diffuse);
                    writer.Write(specular);
                    writer.Write(emissive);
                    writer.Write(power);
                    writer.Write(hasReflection);

                    // ALWAYS write hasImage flag — 1 if texture exists, 0 if not
                    if (textureName != null)
                    {
                        writer.Write(1); // has image

                        // Normalize texture name — add extension if missing
                        if (!textureName.EndsWith(".bmp") && !textureName.EndsWith(".png"))
                        {
                            if (textureName == "BlueChecker" || textureName == "BrightGreenChecker" || textureName == "GreenChecker" ||
                                textureName == "OrangeChecker" || textureName == "PinkChecker" || textureName == "PurpleChecker" ||
                                textureName == "RedChecker")
                            {
                                textureName = textureName + ".bmp";
                            }
                            else
                            {
                                textureName = textureName + ".png";
                            }
                        }

                        writer.Write(textureName ?? "");
                    }
                    else
                    {
                        writer.Write(0); // no image — ALWAYS write this flag!
                    }
                }
                else
                {
                    writer.Write(0); // Does not have color
                }
            }
        }

        private static Vector4 ReadVec4FromExtras(SharpGLTF.IO.JsonDictionary dict, string key, Vector4 defaultValue)
        {
            if (dict.ContainsKey(key))
            {
                var val = dict[key];
                // Array format: [x, y, z, w] (new — Blender float arrays)
                if (val is IList<object> arr && arr.Count >= 4)
                {
                    return new Vector4(
                        Convert.ToSingle(arr[0]),
                        Convert.ToSingle(arr[1]),
                        Convert.ToSingle(arr[2]),
                        Convert.ToSingle(arr[3]));
                }
                // Dict format: {"x":.., "y":.., "z":.., "w":..} (old — Python dict)
                var subDict = val as SharpGLTF.IO.JsonDictionary;
                if (subDict != null)
                {
                    return new Vector4(
                        Convert.ToSingle(subDict["x"]),
                        Convert.ToSingle(subDict["y"]),
                        Convert.ToSingle(subDict["z"]),
                        Convert.ToSingle(subDict["w"]));
                }
            }
            return defaultValue;
        }

        private static Vector3 ReadVec3FromExtras(SharpGLTF.IO.JsonDictionary dict, string key, Vector3 defaultValue)
        {
            if (dict.ContainsKey(key))
            {
                var val = dict[key];
                // Array format: [x, y, z] (new — Blender float arrays)
                if (val is IList<object> arr && arr.Count >= 3)
                {
                    return new Vector3(
                        Convert.ToSingle(arr[0]),
                        Convert.ToSingle(arr[1]),
                        Convert.ToSingle(arr[2]));
                }
                // Dict format: {"x":.., "y":.., "z":..} or {"r":.., "g":.., "b":..} (old — Python dict)
                var subDict = val as SharpGLTF.IO.JsonDictionary;
                if (subDict != null)
                {
                    // Try x/y/z first, then r/g/b
                    float x = subDict.ContainsKey("x") ? Convert.ToSingle(subDict["x"]) :
                              subDict.ContainsKey("r") ? Convert.ToSingle(subDict["r"]) : defaultValue.X;
                    float y = subDict.ContainsKey("y") ? Convert.ToSingle(subDict["y"]) :
                              subDict.ContainsKey("g") ? Convert.ToSingle(subDict["g"]) : defaultValue.Y;
                    float z = subDict.ContainsKey("z") ? Convert.ToSingle(subDict["z"]) :
                              subDict.ContainsKey("b") ? Convert.ToSingle(subDict["b"]) : defaultValue.Z;
                    return new Vector3(x, y, z);
                }
            }
            return defaultValue;
        }

        private static void WriteSplines(CustomWriter writer, ModelRoot model)
        {
            List<spline> Splines = new List<spline>();
            foreach (var Node in model.LogicalNodes)
            {
                if (Node.Name.StartsWith("C:"))
                {
                    spline spline = new spline();
                    spline.name = Node.Name;
                    spline.points = new List<Vertex>();
                    List<Node> ChildNodes = model.LogicalNodes.Where(item => item.VisualParent?.Name == spline.name).OrderBy(item => item.Name).ToList();
                    if (ChildNodes.Count == 0 && Node.Mesh != null)
                    {
                        foreach (MeshPrimitive Primitive in Node.Mesh.Primitives)
                        {
                            GetVertexBuffer(Primitive, out List<Vector3> Vertices);
                            // FIX: Actually use the sorted result (was dead code before)
                            Vertices = Vertices.OrderBy(v => -v.Y).ToList();
                            foreach (Vector3 vertex in Vertices)
                            {
                                Vector3 RPos = vertex;
                                Vector3 NPos = Node.WorldMatrix.Translation;
                                Vector3 Pos = RPos + NPos;
                                Vertex v = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();
                                spline.points.Add(v);
                            }
                        }
                        Splines.Add(spline);
                    }
                    else if (ChildNodes.Count != 0)
                    {
                        foreach (Node node in ChildNodes)
                        {
                            Vector3 Pos = node.WorldMatrix.Translation;
                            Vertex v = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();
                            spline.points.Add(v);
                        }
                        Splines.Add(spline);
                    }
                }
            }
            writer.Write(Splines.Count);
            foreach (var spline in Splines)
            {
                // Strip "C:" prefix (2 chars)
                writer.Write(spline.name.Substring(2));
                writer.Write(spline.points.Count);
                foreach (Vertex v in spline.points)
                {
                    writer.Write(v.X);
                    writer.Write(v.Y);
                    writer.Write(v.Z);
                }
            }
        }

        private static void WriteLights(CustomWriter writer, ModelRoot model)
        {
            // Collect ALL nodes that have a PunctualLight attached, regardless of name.
            var lightNodes = model.LogicalNodes
                .Where(n => n.PunctualLight != null)
                .ToList();

            int LightCount = lightNodes.Count;
            writer.Write(LightCount);

            foreach (var lightNode in lightNodes)
            {
                // Light type — read from extras, default to 0
                int lightType = 0;

                var lightExtras = lightNode.TryUseExtrasAsDictionary(false);
                if (lightExtras != null && lightExtras.ContainsKey("type"))
                {
                    lightType = Convert.ToInt32(lightExtras["type"]);
                }

                writer.Write(lightType);

                // Position
                Vector3 lightPos = lightNode.WorldMatrix.Translation;
                writer.Write(lightPos.X * 50.0f);
                writer.Write(lightPos.Y * 50.0f);
                writer.Write(-lightPos.Z * 50.0f);

                // Direction — computed from the light node's rotation.
                // glTF lights point down -Z. Transform -Z by the node's world rotation.
                Matrix4x4 world = lightNode.WorldMatrix;
                Vector3 forward = new Vector3(0, 0, -1);
                Vector3 dir = Vector3.TransformNormal(forward, world);
                if (dir.Length() < 0.0001f)
                {
                    dir = new Vector3(0, 0, -1);
                }
                dir = Vector3.Normalize(dir);
                // MESHWORLD direction is a point in space, not a unit vector.
                // Store as position + direction (a point some distance away).
                Vector3 dirPos = lightPos + dir;

                writer.Write(dirPos.X * 50.0f);
                writer.Write(dirPos.Y * 50.0f);
                writer.Write(-dirPos.Z * 50.0f);

                // Color — from PunctualLight (Blender's Light Properties panel)
                Vector3 lightColor = lightNode.PunctualLight.Color;
                writer.Write(lightColor.X);
                writer.Write(lightColor.Y);
                writer.Write(lightColor.Z);
            }
        }

        private static void WriteBackgroundAndAmbient(CustomWriter writer, Vector3 background, Vector3 ambient)
        {
            writer.Write(background.X);
            writer.Write(background.Y);
            writer.Write(background.Z);
            writer.Write(ambient.X);
            writer.Write(ambient.Y);
            writer.Write(ambient.Z);
        }

        private static void WriteVertices(CustomWriter writer, ModelRoot model, Vector3 rootMin, Vector3 rootMax)
        {
            List<Vertex> verts = BuildVertList(model, out List<mesh> meshes);
            writer.Write(verts.Count);
            foreach (Vertex v in verts)
            {
                writer.Write(v);
            }

            // Root bounding cube — use stored values if available
            writer.Write(rootMin.X);
            writer.Write(rootMin.Y);
            writer.Write(rootMin.Z);
            writer.Write(rootMax.X);
            writer.Write(rootMax.Y);
            writer.Write(rootMax.Z);

            writer.Write(meshes.Count);

            foreach (mesh m in meshes)
            {
                // Per-mesh bounding box — use root bounds as placeholder
                // (original game uses these for culling, large values are safe)
                writer.Write(rootMin.X);
                writer.Write(rootMin.Y);
                writer.Write(rootMin.Z);
                writer.Write(rootMax.X);
                writer.Write(rootMax.Y);
                writer.Write(rootMax.Z);

                writer.Write(0); // 0 children
                writer.Write(m.geoms.Count);

                foreach (geom g in m.geoms)
                {
                    // Write the geom name (not the mesh name)
                    writer.Write(g.name ?? m.name);

                    writer.Write(g.ambient);   // ambient — now uses actual value
                    writer.Write(g.diffuse);    // diffuse
                    writer.Write(g.specular);   // specular — now uses actual value
                    writer.Write(g.emissive);  // emissive — now uses actual value
                    writer.Write(g.power);      // power/shininess
                    writer.Write(g.hasReflection); // has reflection — now uses actual value

                    if (g.texture != null)
                    {
                        writer.Write(1);
                        writer.Write(g.texture);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    writer.Write(g.strips.Count);

                    foreach (strip s in g.strips)
                    {
                        writer.Write(s.triangleCount);
                        writer.Write(s.vertexOffset);
                    }
                }
            }
        }

        private static List<Vertex> BuildVertList(ModelRoot Root, out List<mesh> meshes)
        {
            List<Vertex> verts = new List<Vertex>();
            meshes = new List<mesh>();

            foreach (var Node in Root.LogicalNodes)
            {
                if (Node.Mesh == null)
                {
                    continue;
                }

                if (!Node.Name.StartsWith("REF:") && !Node.Mesh.Name.StartsWith("C:") && !Node.Name.StartsWith("C:") &&
                    Node.PunctualLight == null)
                {
                    Mesh Mesh = Node.Mesh;

                    mesh m = new mesh();
                    m.name = Node.Name;
                    m.geoms = new List<geom>();
                    foreach (MeshPrimitive Primitive in Mesh.Primitives)
                    {
                        geom g = new geom();
                        // Strip Blender-style ".001", ".002" suffixes from geom names.
                        // The game uses exact string matching for event names (E:LIMIT, E:JUMP, etc.)
                        // so "E:LIMIT.001" would NOT match "E:LIMIT" and the event would be ignored.
                        g.name = StripBlenderSuffix(Node.Name);
                        g.strips = new List<strip>();

                        g.diffuse = Primitive.Material?.FindChannel("BaseColor")?.Parameter ?? Vector4.One;
                        g.emissive = Primitive.Material?.FindChannel("Emissive")?.Parameter ?? Vector4.Zero;

                        // Specular/power/ambient/hasReflection:
                        // Check material extras first (thorough extraction), fall back to PBR sliders (simple mode / Blender)
                        var matExtras = Primitive.Material?.TryUseExtrasAsDictionary(false);

                        if (matExtras != null && matExtras.ContainsKey("ambient"))
                        {
                            // Thorough mode: read from material extras
                            g.ambient = ReadVec4FromExtras(matExtras, "ambient", g.diffuse);
                            g.specular = ReadVec4FromExtras(matExtras, "specular", Vector4.Zero);
                            if (matExtras.ContainsKey("power"))
                                g.power = Convert.ToSingle(matExtras["power"]);
                            if (matExtras.ContainsKey("hasReflection"))
                                g.hasReflection = Convert.ToInt32(matExtras["hasReflection"]);
                        }
                        else
                        {
                            // Simple mode: derive from Blender's metallic/roughness sliders
                            var metRoughChannel = Primitive.Material?.FindChannel("MetallicRoughness");

                            float metallic = 0.0f;
                            float roughness = 1.0f;

                            if (metRoughChannel != null)
                            {
                                metallic = metRoughChannel.Value.Parameter.X;
                                roughness = metRoughChannel.Value.Parameter.Y;
                            }

                            g.ambient = g.diffuse;
                            g.specular = new Vector4(metallic, metallic, metallic, 1.0f);
                            g.power = Math.Max(1.0f, (1.0f - roughness) * 100.0f);
                            g.hasReflection = 0;
                        }

                        var texture = Primitive.Material?.FindChannel("BaseColor")?.Texture;
                        if (texture != null)
                        {
                            string texName = texture.PrimaryImage?.Name;
                            if (!string.IsNullOrEmpty(texName))
                            {
                                // Strip extension if present — extractor stores names without extension
                                texName = Path.GetFileNameWithoutExtension(texName);

                                if (texName == "BlueChecker" || texName == "BrightGreenChecker" || texName == "GreenChecker" ||
                                    texName == "OrangeChecker" || texName == "PinkChecker" || texName == "PurpleChecker" ||
                                    texName == "RedChecker")
                                {
                                    g.texture = texName + ".bmp";
                                }
                                else
                                {
                                    g.texture = texName + ".png";
                                }
                            }
                        }

                        GetVertexBuffer(Primitive, out List<Vector3> Vertices);
                        GetNormalBuffer(Primitive, out List<Vector3> Normals);
                        GetTexCoordBuffer(Primitive, out List<Vector2> Uvs);
                        Vector3[] vs = Vertices.ToArray();
                        Vector3[] ns = Normals.ToArray();
                        Vector2[] uvs = null;
                        if (texture != null)
                        {
                            uvs = Uvs.ToArray();
                        }
                        GetIndexBuffer(Primitive, out List<(int A, int B, int C)> Indices);

                        // Stripify!!
                        List<List<int>> strips = GenerateVertexStrips(Indices);

                        foreach (var stripVerts in strips)
                        {
                            g.strips.Add(new strip { triangleCount = stripVerts.Count - 2, vertexOffset = verts.Count });

                            foreach (int p in stripVerts)
                            {
                                Vector4 Pos = new Vector4(vs[p].X, vs[p].Y, vs[p].Z, 1);
                                Pos = Vector4.Transform(Pos, Node.WorldMatrix);

                                if (texture != null)
                                {
                                    verts.Add(new Vertex
                                    {
                                        X = Pos.X,
                                        Y = Pos.Y,
                                        Z = Pos.Z,
                                        NX = ns[p].X,
                                        NY = ns[p].Y,
                                        NZ = ns[p].Z,
                                        U = uvs[p].X,
                                        V = uvs[p].Y
                                    }.Converted());
                                }
                                else
                                {
                                    verts.Add(new Vertex
                                    {
                                        X = Pos.X,
                                        Y = Pos.Y,
                                        Z = Pos.Z,
                                        NX = ns[p].X,
                                        NY = ns[p].Y,
                                        NZ = ns[p].Z,
                                        U = 1.0f,
                                        V = 1.0f
                                    }.Converted());
                                }
                            }
                        }

                        m.geoms.Add(g);
                    }
                    meshes.Add(m);
                }
            }

            return verts;
        }

        private static List<List<int>> GenerateVertexStrips(List<(int A, int B, int C)> triangles)
        {
            List<List<int>> vertexStrips = new List<List<int>>();
            HashSet<int> unvisited = new HashSet<int>(Enumerable.Range(0, triangles.Count));

            Dictionary<(int, int), List<int>> edgeToTriangles = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                AddEdge(edgeToTriangles, t.A, t.B, i);
                AddEdge(edgeToTriangles, t.B, t.C, i);
                AddEdge(edgeToTriangles, t.C, t.A, i);
            }

            while (unvisited.Count > 0)
            {
                int bestStartTri = -1;
                int minNeighbors = int.MaxValue;

                foreach (int tIndex in unvisited)
                {
                    int neighborCount = 0;
                    var t = triangles[tIndex];
                    neighborCount += CountUnvisited(edgeToTriangles, t.A, t.B, unvisited, tIndex);
                    neighborCount += CountUnvisited(edgeToTriangles, t.B, t.C, unvisited, tIndex);
                    neighborCount += CountUnvisited(edgeToTriangles, t.C, t.A, unvisited, tIndex);

                    if (neighborCount < minNeighbors)
                    {
                        minNeighbors = neighborCount;
                        bestStartTri = tIndex;
                    }
                }

                var startTri = triangles[bestStartTri];

                List<int> bestStrip = null;
                List<int> bestTriPath = null;

                int[][] startPermutations = new int[][] {
                    new int[] { startTri.C, startTri.B, startTri.A },
                    new int[] { startTri.B, startTri.A, startTri.C },
                    new int[] { startTri.A, startTri.C, startTri.B }
                };

                foreach (var startPerm in startPermutations)
                {
                    List<int> currentStrip = new List<int>(startPerm);
                    List<int> currentTriPath = new List<int> { bestStartTri };
                    HashSet<int> tempVisited = new HashSet<int> { bestStartTri };

                    while (true)
                    {
                        int vA = currentStrip[currentStrip.Count - 2];
                        int vB = currentStrip[currentStrip.Count - 1];
                        var edgeKey = (Math.Min(vA, vB), Math.Max(vA, vB));

                        int nextTriIndex = -1;

                        if (edgeToTriangles.ContainsKey(edgeKey))
                        {
                            foreach (int nTri in edgeToTriangles[edgeKey])
                            {
                                if (unvisited.Contains(nTri) && !tempVisited.Contains(nTri))
                                {
                                    nextTriIndex = nTri;
                                    break;
                                }
                            }
                        }

                        if (nextTriIndex != -1)
                        {
                            var nextTri = triangles[nextTriIndex];
                            int vNew = nextTri.A;
                            if (nextTri.B != vA && nextTri.B != vB) vNew = nextTri.B;
                            if (nextTri.C != vA && nextTri.C != vB) vNew = nextTri.C;

                            currentStrip.Add(vNew);
                            currentTriPath.Add(nextTriIndex);
                            tempVisited.Add(nextTriIndex);
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (bestStrip == null || currentStrip.Count > bestStrip.Count)
                    {
                        bestStrip = currentStrip;
                        bestTriPath = currentTriPath;
                    }
                }

                vertexStrips.Add(bestStrip);
                foreach (int tIndex in bestTriPath)
                {
                    unvisited.Remove(tIndex);
                }
            }

            return vertexStrips;
        }

        private static void AddEdge(Dictionary<(int, int), List<int>> dict, int v1, int v2, int triIndex)
        {
            var key = (Math.Min(v1, v2), Math.Max(v1, v2));
            if (!dict.ContainsKey(key)) dict[key] = new List<int>();
            dict[key].Add(triIndex);
        }

        private static int CountUnvisited(Dictionary<(int, int), List<int>> dict, int v1, int v2, HashSet<int> unvisited, int selfTri)
        {
            var key = (Math.Min(v1, v2), Math.Max(v1, v2));
            if (!dict.ContainsKey(key)) return 0;
            return dict[key].Count(t => t != selfTri && unvisited.Contains(t));
        }

        /// <summary>
        /// Strips Blender-style numeric suffixes (.001, .002, etc.) from a name.
        /// The game uses exact string matching for event names (E:LIMIT, N:GOAL, etc.),
        /// so "E:LIMIT.001" must be cleaned to "E:LIMIT" for the event to trigger.
        /// </summary>
        private static string StripBlenderSuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            int dotIdx = name.LastIndexOf(".");
            if (dotIdx > 0 && dotIdx + 1 < name.Length)
            {
                string afterDot = name.Substring(dotIdx + 1);
                bool isNumeric = true;
                foreach (char c in afterDot)
                {
                    if (!char.IsDigit(c)) { isNumeric = false; break; }
                }
                if (isNumeric) return name.Substring(0, dotIdx);
            }
            return name;
        }

        private static bool GetVertexBuffer(MeshPrimitive Primitive, out List<Vector3> VertexBuffer)
        {
            VertexBuffer = Primitive.GetVertexAccessor("POSITION")?.AsVector3Array().ToList();
            if (VertexBuffer?.Count < 3 || Primitive.DrawPrimitiveType != SharpGLTF.Schema2.PrimitiveType.TRIANGLES)
            {
                return false;
            }
            return true;
        }

        private static bool GetIndexBuffer(MeshPrimitive Primitive, out List<(int A, int B, int C)> IndexBuffer)
        {
            IndexBuffer = Primitive.GetTriangleIndices().ToList();
            if (IndexBuffer?.Count == 0)
            {
                return false;
            }
            return true;
        }

        private static bool GetNormalBuffer(MeshPrimitive Primitive, out List<Vector3> NormalBuffer)
        {
            NormalBuffer = Primitive.GetVertexAccessor("NORMAL")?.AsVector3Array().ToList();

            if (NormalBuffer?.Count == 0)
            {
                return false;
            }
            return true;
        }

        private static bool GetTexCoordBuffer(MeshPrimitive Primitive, out List<Vector2> TexCoordBuffer)
        {
            TexCoordBuffer = Primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array().ToList();

            if (TexCoordBuffer?.Count == 0)
            {
                return false;
            }

            return true;
        }
    }
}
