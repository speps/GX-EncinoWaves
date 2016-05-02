using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class QuadTree : MonoBehaviour
{
    public Vector2 extent;
    public Mesh mesh;
    public Material material;

    public class Node
    {
        public Vector2 center;
        public Vector2 extent;
        public Node[] node = new Node[4];
        public int level;

        public void Insert(Vector2 point)
        {
            if (level > 4)
            {
                return;
            }

            var min = center - extent / 2.0f;
            var max = center + extent / 2.0f;
            if (point.x < min.x || point.x > max.x || point.y < min.y || point.y > max.y)
            {
                return;
            }

            if (point.x <= center.x && point.y <= center.y)
            {
                node[0] = new Node
                {
                    center = center + new Vector2(-extent.x / 4, -extent.y / 4),
                    extent = extent / 2,
                    level = level + 1
                };
            }
            else if (point.x > center.x && point.y <= center.y)
            {
                node[1] = new Node
                {
                    center = center + new Vector2(extent.x / 4, -extent.y / 4),
                    extent = extent / 2,
                    level = level + 1
                };
            }
            else if (point.x <= center.x && point.y > center.y)
            {
                node[2] = new Node
                {
                    center = center + new Vector2(-extent.x / 4, extent.y / 4),
                    extent = extent / 2,
                    level = level + 1
                };
            }
            else if (point.x > center.x && point.y > center.y)
            {
                node[3] = new Node
                {
                    center = center + new Vector2(extent.x / 4, extent.y / 4),
                    extent = extent / 2,
                    level = level + 1
                };
            }
            for (int i = 0; i < node.Length; i++)
            {
                if (node[i] != null)
                {
                    node[i].Insert(point);
                }
            }
        }
    }

    void OnEnable()
    {
        // Mesh
        {
            mesh = new Mesh();

            int meshSize = 32;
            float spacing = 1.0f / meshSize;
            float offset = -0.5f;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y <= meshSize; y++)
            {
                for (int x = 0; x <= meshSize; x++)
                {
                    vertices.Add(new Vector3(offset + x * spacing, 0.0f, offset + y * spacing));
                    uvs.Add(new Vector2((float)x / (meshSize - 1), (float)y / (meshSize - 1)));
                }
            }

            var triangles = new List<int>();
            for (int y = 0; y < meshSize; y++)
            {
                for (int x = 0; x < meshSize; x++)
                {
                    var i0 = y * (meshSize + 1) + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + meshSize + 1;
                    var i3 = i2 + 1;

                    Debug.Log(string.Format("{0},{1},{2} {3},{4},{5}", i1, i0, i2, i1, i2, i3));

                    triangles.Add(i1);
                    triangles.Add(i0);
                    triangles.Add(i2);

                    triangles.Add(i1);
                    triangles.Add(i2);
                    triangles.Add(i3);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.UploadMeshData(false);
        }
    }

    void OnPreRender()
    {
        GL.wireframe = true;
    }

    void OnPostRender()
    {
        GL.wireframe = false;
    }

    void Update()
    {
        var camera = GetComponent<Camera>();
        var root = new Node()
        {
            center = new Vector2(0, 0),
            extent = extent
        };
        root.Insert(new Vector2(camera.transform.position.x, camera.transform.position.z));

        Graphics.DrawMesh(mesh, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(extent.x, 1.0f, extent.y)), material, 0);

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var min = current.center - current.extent / 2;
            var max = current.center + current.extent / 2;
            Debug.DrawLine(new Vector3(min.x, 0, min.y), new Vector3(max.x, 0, min.y));
            Debug.DrawLine(new Vector3(max.x, 0, min.y), new Vector3(max.x, 0, max.y));
            Debug.DrawLine(new Vector3(max.x, 0, max.y), new Vector3(min.x, 0, max.y));
            Debug.DrawLine(new Vector3(min.x, 0, max.y), new Vector3(min.x, 0, min.y));
            var scale = new Vector3(current.extent.x / 2.0f, 1.0f, current.extent.y / 2.0f);
            if (current.node[0] == null)
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(current.center.x - scale.x / 2.0f, 0.0f, current.center.y - scale.z / 2.0f), Quaternion.identity, scale), material, 0);
            if (current.node[1] == null)
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(current.center.x + scale.x / 2.0f, 0.0f, current.center.y - scale.z / 2.0f), Quaternion.identity, scale), material, 0);
            if (current.node[2] == null)
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(current.center.x - scale.x / 2.0f, 0.0f, current.center.y + scale.z / 2.0f), Quaternion.identity, scale), material, 0);
            if (current.node[3] == null)
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(current.center.x + scale.x / 2.0f, 0.0f, current.center.y + scale.z / 2.0f), Quaternion.identity, scale), material, 0);
            for (int i = 0; i < current.node.Length; i++)
            {
                if (current.node[i] != null)
                {
                    stack.Push(current.node[i]);
                }
            }
        }
    }
}
