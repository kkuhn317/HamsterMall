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
}