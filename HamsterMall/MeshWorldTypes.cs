using System.Collections.Generic;
using System.Numerics;

namespace HamsterMall
{
    struct Vertex
    {
        public float X, Y, Z, NX, NY, NZ, U, V;

        public Vertex Converted()
        {
            return new Vertex { X = X * 50.0f, Y = Y * 50.0f, Z = -Z * 50.0f, NX = NX, NY = NY, NZ = -NZ, U = U, V = V };
        }

        public Vertex Unconverted()
        {
            return new Vertex { X = X / 50.0f, Y = Y / 50.0f, Z = -Z / 50.0f, NX = NX, NY = NY, NZ = -NZ, U = U, V = V };
        }
    }


    public class mesh
    {
        public string name;
        public List<geom> geoms;
        public List<mesh> children = new List<mesh>();
    }

    public struct geom
    {
        public string name;
        public Vector4 ambient;
        public Vector4 diffuse;
        public Vector4 specular;
        public Vector4 emissive;
        public float power;
        public int hasReflection;
        public string texture;
        public List<strip> strips;
    }

    public struct strip
    {
        public int triangleCount;
        public int vertexOffset;
    }

    struct spline
    {
        public string name;
        public List<Vertex> points;
    }

    public class RefPoint
    {
        public string name;
        public System.Numerics.Vector3 position;
        public System.Numerics.Vector3 rotation; // Euler angles in degrees (RotZ, RotY, RotX)
        public geom properties;
    }

    public class LightObj
    {
        public int type;
        public System.Numerics.Vector3 position;
        public System.Numerics.Vector3 direction;
        public System.Numerics.Vector3 color;
    }
}
