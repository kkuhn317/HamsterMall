using SharpGLTF.Runtime;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HamsterMall
{

    struct Vertex
    {
        public float X, Y, Z, NX, NY, NZ, U, V;

        public Vertex Converted()
        {
            return new Vertex { X = X * 50.0f, Y = Y * 50.0f, Z = -Z * 50.0f, NX = NX, NY = NY, NZ = -NZ, U = U, V = V };
        }
    }


    struct mesh
    {
        public string name;
        public List<geom> geoms;

    }

    struct geom
    {
        public Vector4 ambient;
        public Vector4 diffuse;
        public Vector4 specular;
        public Vector4 emissive;
        public float power;
        public int hasReflection;
        public string texture;
        public List<strip> strips;
    }

    struct strip
    {
        public int triangleCount;
        public int vertexOffset;
    }

    struct spline
    {
        public string name;
        public List<Vertex> points;
    }

    public partial class HamsterMall : Form
    {
        public HamsterMall()
        {
            InitializeComponent();
        }


        private void Ambient_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                Ambient.BackColor = colorDialog1.Color;
            }
        }

        private void Background_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                Background.BackColor = colorDialog1.Color;
            }
        }


        private void loadButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                meshFileText.Text = openFileDialog1.FileName;
            }

        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.FileName != null && saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                using (FileStream saveFile = File.OpenWrite(saveFileDialog1.FileName))
                {
                    using (CustomWriter writer = new CustomWriter(saveFile))
                    {

                        var model = SharpGLTF.Schema2.ModelRoot.Load(openFileDialog1.FileName);

                        WriteRefPoints(writer, model);
                        WriteSplines(writer, model);
                        WriteLights(writer, model);
                        WriteBackgroundAndAmbient(writer);
                        WriteVertices(writer, model);

                        var saveFileInfo = new FileInfo(saveFileDialog1.FileName);
                        var textureDirectoryPath = Path.Combine(saveFileInfo.DirectoryName, "textures");
                        EnsureClearDirectory(textureDirectoryPath);
                        WriteTextures(model, textureDirectoryPath);
                    }
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

        private void WriteTextures(ModelRoot model, string textureDirectoryPath)
        {
            var textures = model.LogicalNodes
                .SelectMany(node => node.Mesh?.Primitives ?? Enumerable.Empty<MeshPrimitive>())
                .Select(primitive => primitive.Material?.Channels?.FirstOrDefault(channel => channel.Key == "BaseColor").Texture)
                .Where(texture => texture != null)
                .GroupBy(texture => texture.PrimaryImage.Name)
                .Select(texture => texture.First());

            foreach (var texture in textures)
            {
                var image = texture.PrimaryImage;
                var pngBytes = image.Content.Content.ToArray();
                var pngPath = Path.Combine(textureDirectoryPath, image.Name + ".png");
                File.WriteAllBytes(pngPath, pngBytes);
            }
        }

        private void WriteRefPoints(CustomWriter writer, ModelRoot model)
        {
            var Nodes = new List<Node>();
            foreach(Node node in model.LogicalNodes)
            {
                if (!node.Name.StartsWith("C:") && !node.Name.StartsWith("Light") && !node.Name.StartsWith("Direction"))
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

            writer.Write(Nodes.Count);

            foreach (var node in Nodes)
            {
                int length = node.Name.LastIndexOf(".");
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
                
                length = length == -1 ? node.Name.Length : length;

                writer.Write(node.Name.Substring(startLength, length-startLength));
                writer.Write(node.WorldMatrix.Translation.X * 50.0f);
                writer.Write(node.WorldMatrix.Translation.Y * 50.0f);
                writer.Write(-node.WorldMatrix.Translation.Z * 50.0f);

                //This is all code translating the quaternion rotation format into the Euler format
                if(true)
                {
                    
                    double rY = node.LocalTransform.Rotation.X;
                    double rX = node.LocalTransform.Rotation.Y;
                    double rZ = -node.LocalTransform.Rotation.Z;
                    double rW = node.LocalTransform.Rotation.W;

                    double RotX = 0;
                    double RotY = 0;
                    double RotZ = 0;

                    if (1 - 2 * (rX * rX + rY * rY) != 0)
                    {
                        RotY = 180 * Math.Atan2(2 * (rW * rX + rY * rZ), (1 - 2 * (rX * rX + rY * rY))) / Math.PI;
                    }

                    if (1 - 2 * (rY * rY + rZ * rZ) != 0)
                    {
                        RotZ = 180 * Math.Atan2(2 * (rW * rZ + rX * rY), (1 - 2 * (rY * rY + rZ * rZ))) / Math.PI;
                    }
                    RotX = 180 * Math.Asin(2 * (rW * rY - rZ * rX)) / Math.PI;

                    if (Double.IsNaN(RotY))
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
                    writer.Write((float)RotZ);//Rotation Z
                    writer.Write((float)RotY);//Rotation Y
                    writer.Write((float)RotX);//Rotation X
                }
                //End of code to write rotation

                
                if (REF)
                {
                    writer.Write(1); //Has color

                    writer.Write(0.9921f);
                    writer.Write(0.9921f);
                    writer.Write(0.9921f);
                    writer.Write(1f);

                    writer.Write(0.9921f);
                    writer.Write(0.9921f);
                    writer.Write(0.9921f);
                    writer.Write(1f);

                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(1f);

                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(1f);

                    writer.Write(10f); // power?
                    writer.Write(0); //has reflection

                    var Primitive = node.Mesh.Primitives;
                    var texture = Primitive[0].Material?.Channels?.FirstOrDefault(channel => channel.Key == "BaseColor").Texture;
                    if (texture != null)
                    {
                        writer.Write(1); //has image
                        string texture2 = texture.PrimaryImage.Name;
                        if (!texture2.EndsWith(".bmp") && !texture2.EndsWith(".png"))
                        {
                            if (texture.PrimaryImage.Name == "BlueChecker" || texture.PrimaryImage.Name == "BrightGreenChecker" || texture.PrimaryImage.Name == "GreenChecker" || texture.PrimaryImage.Name == "OrangeChecker" || texture.PrimaryImage.Name == "PinkChecker" || texture.PrimaryImage.Name == "PurpleChecker" || texture.PrimaryImage.Name == "RedChecker")
                            {
                                texture2 = texture.PrimaryImage.Name + ".bmp";
                            }
                            else
                            {
                                texture2 = texture.PrimaryImage.Name + ".png";
                            }
                        }
                        else 
                        {
                            texture2 = texture.PrimaryImage.Name;
                        }
                        
                        
                        
                        writer.Write(texture2 ?? "");
                    }
                }
                else
                {
                    writer.Write(0);//Does not have color
                }
            }



        }


        private void WriteSplines(CustomWriter writer, ModelRoot model)
        {
            
            List<spline> Splines = new List<spline>();
            foreach (var Node in model.LogicalNodes)
            {
                if(Node.Name.StartsWith("C:"))
                {
                    spline spline = new spline();
                    spline.name = Node.Name;
                    spline.points = new List<Vertex>();
                    List<Node> ChildNodes = model.LogicalNodes.Where(item => item.VisualParent?.Name == spline.name).OrderBy(item => item.Name).ToList();
                    if(ChildNodes.Count == 0 && Node.Mesh != null)
                    {
                        foreach (MeshPrimitive Primitive in Node.Mesh.Primitives)
                        {
                            GetVertexBuffer(Primitive, out List<Vector3> Vertices);
                            Vertices.OrderBy(Vertex => -Vertex.Y);
                            foreach(Vector3 vertex in Vertices)
                            {
                                //convert to proper coordinates
                                Vector3 RPos = vertex; //Relative position to node
                                Vector3 NPos = Node.WorldMatrix.Translation; //Node position
                                Vector3 Pos = RPos + NPos; //Real position
                                Vertex v = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();
                                
                                //add to spline.points
                                spline.points.Add(v);
                            }
                            
                        }
                        Splines.Add(spline);
                    }
                    else if (ChildNodes.Count != 0)
                    {
                        foreach(Node node in ChildNodes)
                        {
                            //convert nodes to vertices
                            Vector3 Pos = node.WorldMatrix.Translation;
                            Vertex v = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();

                            //add to spline.points
                            spline.points.Add(v);
                        }
                        Splines.Add(spline);
                    }
                    
                    
                }
            }
            writer.Write(Splines.Count);//number of splines
            foreach (var spline in Splines)
            {
                int length = spline.name.Length;
                writer.Write(spline.name.Substring(2, length-2));//name of spline
                writer.Write(spline.points.Count);//number of points on spline
                foreach(Vertex v in spline.points)
                {
                    writer.Write(v.X);
                    writer.Write(v.Y);
                    writer.Write(v.Z);
                }
            }
            

            //No need to write a spline apparently the camera just follows if i don't populate this at all
            //writer.Write(0);
        }

        private void WriteLights(CustomWriter writer, ModelRoot model)
        {
            
            
            List<Vertex> Lights = new List<Vertex>();
            List<Vertex> Directions = new List<Vertex>();
            foreach (var Node in model.LogicalNodes.OrderBy(node => node.Name))
            {
                if (Node.Name.StartsWith("Light"))
                {
                    Vector3 Pos = Node.WorldMatrix.Translation;
                    Vertex Light = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();
                    Lights.Add(Light);
                }
                else if (Node.Name.StartsWith("Direction"))
                {
                    Vector3 Pos = Node.WorldMatrix.Translation;
                    Vertex Direction = new Vertex { X = Pos.X, Y = Pos.Y, Z = Pos.Z }.Converted();
                    Directions.Add(Direction);
                }
            }

            int LightCount = Lights.Count;
            writer.Write(LightCount);
            
            for (int i = 1; i <= LightCount; i++)
            {
                writer.Write(0);
                writer.Write(Lights[i - 1].X);
                writer.Write(Lights[i - 1].Y);
                writer.Write(Lights[i - 1].Z);
                writer.Write(Directions[i - 1].X);
                writer.Write(Directions[i - 1].Y);
                writer.Write(Directions[i - 1].Z);
                writer.Write(1.0f);
                writer.Write(1.0f);
                writer.Write(1.0f);
            }
            
        }

        private void WriteBackgroundAndAmbient(CustomWriter writer)
        {
            writer.Write(Background.BackColor.R / 255.0f);
            writer.Write(Background.BackColor.G / 255.0f);
            writer.Write(Background.BackColor.B / 255.0f);
            writer.Write(Ambient.BackColor.R / 255.0f);
            writer.Write(Ambient.BackColor.G / 255.0f);
            writer.Write(Ambient.BackColor.B / 255.0f);
        }

        private void WriteVertices(CustomWriter writer, ModelRoot model)
        {

            List<Vertex> verts = BuildVertList(model, out List<mesh> meshes);
            writer.Write(verts.Count);
            foreach (Vertex v in verts)
            {
                writer.Write(v);
            }

    


            //Cube
            writer.Write(-1000000.0f);
            writer.Write(-1000000.0f);
            writer.Write(-1000000.0f);

            writer.Write(1000000.0f);
            writer.Write(1000000.0f);
            writer.Write(1000000.0f);

            writer.Write(meshes.Count); // "submesh" count

            foreach (mesh m in meshes)
            {
                writer.Write(-1000000.0f);
                writer.Write(-1000000.0f);
                writer.Write(-1000000.0f);

                writer.Write(1000000.0f);
                writer.Write(1000000.0f);
                writer.Write(1000000.0f);


                writer.Write(0); // 0 submeshes
                writer.Write(m.geoms.Count); // geom count

                foreach(geom g in m.geoms)
                {
                    int length = m.name.LastIndexOf(".");
                    length = length == -1 ? m.name.Length : length;
                    writer.Write(m.name.Substring(0,length));
                    //If there is no emission property
                    if (g.emissive == Vector4.Zero || g.emissive == new Vector4(0,0,0,1))
                    {
                        if (m.name.StartsWith("T:") && m.name != "T:GOALAREA" && g.texture != "OddArrow.png" && g.texture != "YellowArrow.png")
                        {
                            writer.Write(1.0f);
                            writer.Write(1.0f);
                            writer.Write(1.0f);
                            writer.Write(0.5f);//ambient
                            writer.Write(1.0f);
                            writer.Write(1.0f);
                            writer.Write(1.0f);
                            writer.Write(0.5f);//diffuse
                            writer.Write(g.specular);
                            writer.Write(g.emissive);
                        }
                        else
                        {
                            //writer.Write(Vector4.Zero);//ambient
                            writer.Write(g.diffuse);//ambient
                            writer.Write(g.diffuse);//diffuse
                            writer.Write(g.specular);//spec
                            writer.Write(g.emissive);//emissive
                        }
                    }
                    else //if there is an emission property
                    {
                        if (m.name.StartsWith("T:") && g.texture != null)
                        {
                            if(g.texture == "Decal_Start.png")
                            {
                                writer.Write(g.diffuse);//ambient
                                writer.Write(g.diffuse);//diffuse
                                writer.Write(g.specular);
                                writer.Write(g.emissive);
                            }
                            else if (g.texture == "goal.png" || g.texture == "goal-round.png")
                            {
                                writer.Write(g.emissive);//ambient
                                writer.Write(g.emissive);//diffuse
                                writer.Write(g.specular);
                                writer.Write(g.emissive);
                            }
                            else if (g.texture == "Decal_Warning.png")
                            {
                                writer.Write(0.5882353186607361f);
                                writer.Write(0.5882353186607361f);
                                writer.Write(0.5882353186607361f);
                                writer.Write(1f);//ambient
                                writer.Write(0.5882353186607361f);
                                writer.Write(0.5882353186607361f);
                                writer.Write(0.5882353186607361f);
                                writer.Write(1f);//diffuse
                                writer.Write(g.specular);
                                writer.Write(0.9921569228172302f);
                                writer.Write(0.9921569228172302f);
                                writer.Write(0.9921569228172302f);
                                writer.Write(1f);//emissive
                                
                            }
                            else if (g.texture == "NeonArrow.png")
                            {
                                writer.Write(0.988235354423523f);
                                writer.Write(1f);
                                writer.Write(0);
                                writer.Write(0.75f);//ambient
                                writer.Write(0.988235354423523f);
                                writer.Write(1f);
                                writer.Write(0);
                                writer.Write(0.75f);//diffuse
                                writer.Write(g.specular);
                                writer.Write(g.emissive.X);
                                writer.Write(g.emissive.Y);
                                writer.Write(g.emissive.Z);
                                writer.Write(0.75f);
                            }
                            else
                            {
                                writer.Write(g.diffuse);//ambient
                                writer.Write(g.diffuse);//diffuse
                                writer.Write(g.specular);//spec
                                writer.Write(g.emissive);//emissive
                            }
                        }
                        else
                        {
                            //writer.Write(Vector4.Zero);//ambient
                            writer.Write(g.diffuse);//ambient
                            writer.Write(g.diffuse);//diffuse
                            writer.Write(g.specular);//spec
                            writer.Write(g.emissive);//emissive
                        }
                    }
                    writer.Write(10f); // power?
                    writer.Write(0); //has reflection

                    if (g.texture != null)
                    {
                        writer.Write(1);
                        writer.Write(g.texture);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    writer.Write(g.strips.Count); // strip count

                    foreach(strip s in g.strips)
                    {
                        writer.Write(s.triangleCount);
                        writer.Write(s.vertexOffset);
                    }

                }


            }


        }
        private List<Vertex> BuildVertList(ModelRoot Root, out List<mesh> meshes)
        {
            List<Vertex> verts = new List<Vertex>();
            meshes = new List<mesh>();



            foreach (var Node  in Root.LogicalNodes)
            {
                if(Node.Mesh == null)
                {
                    continue;
                }

                if (!Node.Name.StartsWith("REF:") && !Node.Mesh.Name.StartsWith("C:") && !Node.Name.StartsWith("C:"))
                {

                    Mesh Mesh = Node.Mesh;

                    mesh m = new mesh();
                    m.name = Node.Name;
                    m.geoms = new List<geom>();
                    foreach (MeshPrimitive Primitive in Mesh.Primitives)
                    {
                        geom g = new geom();
                        g.strips = new List<strip>();

                        g.diffuse = Primitive.Material?.Channels?.First(channel => channel.Key == "BaseColor").Parameter ?? Vector4.One;

                        g.emissive = Primitive.Material?.Channels?.First(channel => channel.Key == "Emissive").Parameter ?? Vector4.Zero;

                        g.specular = Vector4.Zero;

                        var texture = Primitive.Material?.Channels?.FirstOrDefault(channel => channel.Key == "BaseColor").Texture;
                        if (texture != null)
                        {
                            if (!texture.PrimaryImage.Name.EndsWith(".png") && !texture.PrimaryImage.Name.EndsWith(".bmp"))
                            {
                                if (texture.PrimaryImage.Name == "BlueChecker" || texture.PrimaryImage.Name == "BrightGreenChecker" || texture.PrimaryImage.Name == "GreenChecker" || texture.PrimaryImage.Name == "OrangeChecker" || texture.PrimaryImage.Name == "PinkChecker" || texture.PrimaryImage.Name == "PurpleChecker" || texture.PrimaryImage.Name == "RedChecker")
                                {
                                    g.texture = texture.PrimaryImage.Name + ".bmp";
                                }
                                else
                                {
                                    g.texture = texture.PrimaryImage.Name + ".png";
                                }
                            }
                            else 
                            {
                                g.texture = texture.PrimaryImage.Name;
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

                        //TODO stripify triangles

                        Console.WriteLine("triangleCount:" + Indices.Count());

                        // OLD WAY
                        //foreach (var tri in Indices)
                        //{
                        //    Console.WriteLine("creating strip with count " + 1 + " and voffset " + verts.Count);
                        //    g.strips.Add(new strip { triangleCount = 1, vertexOffset = verts.Count });
                        //    Vector4 PosC = new Vector4(vs[tri.C].X, vs[tri.C].Y, vs[tri.C].Z, 1);
                        //    Vector4 PosB = new Vector4(vs[tri.B].X, vs[tri.B].Y, vs[tri.B].Z, 1);
                        //    Vector4 PosA = new Vector4(vs[tri.A].X, vs[tri.A].Y, vs[tri.A].Z, 1);
                        //    PosC = Vector4.Transform(PosC, Node.WorldMatrix);
                        //    PosB = Vector4.Transform(PosB, Node.WorldMatrix);
                        //    PosA = Vector4.Transform(PosA, Node.WorldMatrix);
                        //    if (texture != null)
                        //    {
                        //        verts.Add(new Vertex { X = PosC.X, Y = PosC.Y, Z = PosC.Z, NX = ns[tri.C].X, NY = ns[tri.C].Y, NZ = ns[tri.C].Z, U = uvs[tri.C].X, V = uvs[tri.C].Y }.Converted());
                        //        verts.Add(new Vertex { X = PosB.X, Y = PosB.Y, Z = PosB.Z, NX = ns[tri.B].X, NY = ns[tri.B].Y, NZ = ns[tri.B].Z, U = uvs[tri.B].X, V = uvs[tri.B].Y }.Converted());
                        //        verts.Add(new Vertex { X = PosA.X, Y = PosA.Y, Z = PosA.Z, NX = ns[tri.A].X, NY = ns[tri.A].Y, NZ = ns[tri.A].Z, U = uvs[tri.A].X, V = uvs[tri.A].Y }.Converted());
                        //    }
                        //    else
                        //    {
                        //        verts.Add(new Vertex { X = PosC.X, Y = PosC.Y, Z = PosC.Z, NX = ns[tri.C].X, NY = ns[tri.C].Y, NZ = ns[tri.C].Z, U = 1.0f, V = 1.0f }.Converted());
                        //        verts.Add(new Vertex { X = PosB.X, Y = PosB.Y, Z = PosB.Z, NX = ns[tri.B].X, NY = ns[tri.B].Y, NZ = ns[tri.B].Z, U = 1.0f, V = 1.0f }.Converted());
                        //        verts.Add(new Vertex { X = PosA.X, Y = PosA.Y, Z = PosA.Z, NX = ns[tri.A].X, NY = ns[tri.A].Y, NZ = ns[tri.A].Z, U = 1.0f, V = 1.0f }.Converted());
                        //    }
                        //}


                        // Stripify Way
                        List<List<int>> strips = GenerateVertexStrips(Indices);

                        foreach (var stripVerts in strips)
                        {
                            // A valid strip of N vertices ALWAYS produces (N - 2) triangles
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

                        Console.WriteLine("g.strips length: " + g.strips.Count());
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

            // Precompute an edge-to-triangle map so we can instantly find neighbors
            // We use a tuple of (minVertex, maxVertex) so the edge direction doesn't matter
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
                // 1. Find the best starting triangle (the one with the fewest unvisited neighbors)
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

                // 2. A triangle has 3 edges. We try all 3 valid winding permutations 
                // to see which direction yields the longest continuous strip.
                List<int> bestStrip = null;
                List<int> bestTriPath = null;

                // Using your corrected (C, B, A) winding order!
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
                        // The active edge is always the last two vertices
                        int vA = currentStrip[currentStrip.Count - 2];
                        int vB = currentStrip[currentStrip.Count - 1];
                        var edgeKey = (Math.Min(vA, vB), Math.Max(vA, vB));

                        int nextTriIndex = -1;

                        // Find an unvisited triangle that shares this exact edge
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
                            // We found a connecting triangle! Append its unique vertex.
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
                            // Dead end. We can't go any further in this direction.
                            break;
                        }
                    }

                    // If this path was longer than the others, save it
                    if (bestStrip == null || currentStrip.Count > bestStrip.Count)
                    {
                        bestStrip = currentStrip;
                        bestTriPath = currentTriPath;
                    }
                }

                // 3. Commit the longest strip we found and mark its triangles as visited
                vertexStrips.Add(bestStrip);
                foreach (int tIndex in bestTriPath)
                {
                    unvisited.Remove(tIndex);
                }
            }

            return vertexStrips;
        }

        // Helper method to populate the edge dictionary
        private static void AddEdge(Dictionary<(int, int), List<int>> dict, int v1, int v2, int triIndex)
        {
            var key = (Math.Min(v1, v2), Math.Max(v1, v2));
            if (!dict.ContainsKey(key)) dict[key] = new List<int>();
            dict[key].Add(triIndex);
        }

        // Helper method to count unvisited neighbors on a specific edge
        private static int CountUnvisited(Dictionary<(int, int), List<int>> dict, int v1, int v2, HashSet<int> unvisited, int selfTri)
        {
            var key = (Math.Min(v1, v2), Math.Max(v1, v2));
            if (!dict.ContainsKey(key)) return 0;
            return dict[key].Count(t => t != selfTri && unvisited.Contains(t));
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
