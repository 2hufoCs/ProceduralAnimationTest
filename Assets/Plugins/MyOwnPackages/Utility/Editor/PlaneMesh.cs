using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[ExecuteAlways]
public class PlaneMesh : MonoBehaviour
{
    [SerializeField] Vector2 size;
    public Vector2 Size
    {
        get {return size;}
        set
        {
            size = value;
            sizeChangeCheck = value;
            GeneratePlane(size, resolution);
        }
    }
    Vector2 sizeChangeCheck;

    [SerializeField] [Range(1, 50)] int resolution;
    public int Resolution
    {
        get {return resolution;}
        set
        {
            resolution = value;
            resChangeCheck = value;
            GeneratePlane(size, resolution);
        }
    }
    int resChangeCheck;

    Mesh mesh;
    List<Vector3> vertices = new();
    List<int> triangles = new();
    List<Vector3> normals = new();
    List<Vector2> UVs = new();

    bool loaded = false;

    private void Start()
    {
        mesh = GetComponent<MeshFilter>().sharedMesh;
        GeneratePlane(size, resolution);
        if (GetComponent<MeshCollider>()) GetComponent<MeshCollider>().sharedMesh = mesh;
        loaded = true;
    }

    void OnValidate()
    {
        if (!loaded)
        {
            return;
        }

        if (sizeChangeCheck != size)
        {
            Size = size;
        }
        if (resChangeCheck != resolution)
        {
            Resolution = resolution;
        }
    }

    void GeneratePlane(Vector2 size, int resolution)
    {
        vertices = new List<Vector3>();
        UVs = new List<Vector2>();
        triangles = new List<int>();

        // Create vertices
        float xPerStep = size.x / resolution;
        float yPerStep = size.y / resolution;
        for (int y = 0; y  < resolution + 1; y++)
        {
            for (int x = 0; x < resolution + 1; x++)
            {
                int i = y * (resolution+1) + x;
                vertices.Add(new Vector3(x * xPerStep, 0, y * yPerStep));
                UVs.Add(new Vector2(vertices[i].x / size.x, vertices[i].z / size.y));
                normals.Add(Vector3.up);
            }
        }

        // Create triangles
        for (int row = 0; row < resolution; row++)
        {
            for (int col = 0; col < resolution; col++)
            {
                int i = (row * resolution) + row + col;

                // First triangle
                triangles.Add(i);
                triangles.Add(i + resolution + 1);
                triangles.Add(i + resolution + 2);

                // Second triangle
                triangles.Add(i);
                triangles.Add(i + resolution + 2);
                triangles.Add(i + 1);
            }
        }

        // Set everything for the mesh
        AssignMesh();
    }

    void AssignMesh()
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, UVs);
    }
}
