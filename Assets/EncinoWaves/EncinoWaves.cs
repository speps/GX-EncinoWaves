using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class EncinoWaves : MonoBehaviour
{
    private ComputeShader shaderSpectrum;
    private int kernelSpectrumInit;
    private int kernelSpectrumUpdate;

    private ComputeShader shaderFFT;
    private int kernelFFTX = 0;
    private int kernelFFTY = 1;

    private Material materialCombine;

    private int size = 256;
    private int meshSize = 32;
    // Spectrum
    private RenderTexture bufferSpectrumH0;
    private RenderTexture bufferSpectrumH;
    private RenderTexture bufferSpectrumDx;
    private RenderTexture bufferSpectrumDy;
    // Final
    private RenderTexture bufferHFinal;
    private RenderTexture bufferDxFinal;
    private RenderTexture bufferDyFinal;

    // FFT
    private RenderTexture bufferFFTTemp;
    private Texture2D texButterfly;

    // Combine
    private RenderTexture bufferDisplacement;
    private RenderTexture bufferGradientFold;

    public Mesh mesh;
    public Material material;
    public Material materialExtended;
    public float domainSize = 200.0f;
    public float choppiness = 2.0f;
    public bool wireframe;
    public bool debug;

    RenderTexture CreateSpectrumUAV()
    {
        var uav = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        uav.enableRandomWrite = true;
        uav.Create();
        return uav;
    }

    RenderTexture CreateFinalTexture()
    {
        var texture = new RenderTexture(size, size, 0, RenderTextureFormat.RFloat);
        texture.enableRandomWrite = true;
        texture.Create();
        return texture;
    }

    RenderTexture CreateCombinedTexture()
    {
        var texture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        texture.enableRandomWrite = true;
        texture.useMipMap = true;
        texture.generateMips = true;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.Create();
        return texture;
    }

    void OnEnable()
    {
        shaderSpectrum = (ComputeShader)Resources.Load("EncinoSpectrum", typeof(ComputeShader));
        kernelSpectrumInit = shaderSpectrum.FindKernel("EncinoSpectrumInit");
        kernelSpectrumUpdate = shaderSpectrum.FindKernel("EncinoSpectrumUpdate");
        shaderFFT = (ComputeShader)Resources.Load("EncinoFFT", typeof(ComputeShader));
        materialCombine = (Material)Resources.Load("EncinoCombine", typeof(Material));

        bufferFFTTemp = new RenderTexture(size, size, 0, RenderTextureFormat.RGFloat);
        bufferFFTTemp.enableRandomWrite = true;
        bufferFFTTemp.Create();

        bufferSpectrumH0 = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        bufferSpectrumH0.enableRandomWrite = true;
        bufferSpectrumH0.Create();

        bufferSpectrumH = CreateSpectrumUAV();
        bufferSpectrumDx = CreateSpectrumUAV();
        bufferSpectrumDy = CreateSpectrumUAV();
        bufferHFinal = CreateFinalTexture();
        bufferDxFinal = CreateFinalTexture();
        bufferDyFinal = CreateFinalTexture();
        bufferDisplacement = CreateCombinedTexture();
        bufferGradientFold = CreateCombinedTexture();

        // Butterfly
        {
            int log2Size = Mathf.RoundToInt(Mathf.Log(size, 2));
            
            var butterflyData = new Vector2[size * log2Size];

            int offset = 1, numIterations = size >> 1;
            for (int rowIndex = 0; rowIndex < log2Size; rowIndex++)
            {
                int rowOffset = rowIndex * size;

                // Weights
                {
                    int start = 0, end = 2 * offset;
                    for (int iteration = 0; iteration < numIterations; iteration++)
                    {
                        float bigK = 0.0f;
                        for (int K = start; K < end; K += 2)
                        {
                            float phase = 2.0f * Mathf.PI * bigK * numIterations / size;
                            float cos = Mathf.Cos(phase);
                            float sin = Mathf.Sin(phase);

                            butterflyData[rowOffset + K / 2].x = cos;
                            butterflyData[rowOffset + K / 2].y = -sin;

                            butterflyData[rowOffset + K / 2 + offset].x = -cos;
                            butterflyData[rowOffset + K / 2 + offset].y = sin;

                            bigK += 1.0f;
                        }
                        start += 4 * offset;
                        end = start + 2 * offset;
                    }
                }

                numIterations >>= 1;
                offset <<= 1;
            }

            var butterflyBytes = new byte[butterflyData.Length * sizeof(ushort) * 2];
            for (uint i = 0; i < butterflyData.Length; i++)
            {
                uint byteOffset = i * sizeof(ushort) * 2;
                HalfHelper.SingleToHalf(butterflyData[i].x, butterflyBytes, byteOffset);
                HalfHelper.SingleToHalf(butterflyData[i].y, butterflyBytes, byteOffset + sizeof(ushort));
            }

            texButterfly = new Texture2D(size, log2Size, TextureFormat.RGHalf, false);
            texButterfly.LoadRawTextureData(butterflyBytes);
            texButterfly.Apply(false, true);
        }

        // Mesh
        {
            mesh = new Mesh();
            mesh.name = "EncinoMesh";

            float spacing = 1.0f / meshSize;
            var offset = new Vector3(-0.5f, 0.0f, -0.5f);

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y <= meshSize; y++)
            {
                for (int x = 0; x <= meshSize; x++)
                {
                    vertices.Add(offset + new Vector3(x * spacing, 0.0f, y * spacing));
                    uvs.Add(new Vector2((float)x / meshSize, (float)y / meshSize));
                }
                for (int x = 0; x < meshSize; x++)
                {
                    if (y < meshSize)
                    {
                        vertices.Add(offset + new Vector3((x + 0.5f) * spacing, 0.0f, (y + 0.5f) * spacing));
                        uvs.Add(new Vector2((float)(x + 0.5f) / meshSize, (float)(y + 0.5f) / meshSize));
                    }
                }
            }

            var triangles = new List<int>();
            for (int y = 0; y < meshSize; y++)
            {
                for (int x = 0; x < meshSize; x++)
                {
                    var i0 = y * (meshSize * 2 + 1) + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + (meshSize * 2) + 1;
                    var i3 = i2 + 1;
                    var ic = i0 + meshSize + 1;

                    triangles.Add(i1);
                    triangles.Add(i0);
                    triangles.Add(ic);

                    triangles.Add(i1);
                    triangles.Add(ic);
                    triangles.Add(i3);

                    triangles.Add(ic);
                    triangles.Add(i2);
                    triangles.Add(i3);

                    triangles.Add(i0);
                    triangles.Add(i2);
                    triangles.Add(ic);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.UploadMeshData(false);
            mesh.RecalculateNormals();
        }
    }

    void SpectrumInit()
    {
        shaderSpectrum.SetInt("size", size);
        shaderSpectrum.SetFloat("domainSize", domainSize);
        shaderSpectrum.SetFloat("gravity", 9.81f);
        shaderSpectrum.SetFloats("windDirection", Mathf.Sqrt(2), Mathf.Sqrt(2));
        shaderSpectrum.SetFloat("windSpeed", 20.0f);

        shaderSpectrum.SetTexture(kernelSpectrumInit, "outputH0", bufferSpectrumH0);

        shaderSpectrum.Dispatch(kernelSpectrumInit, size / 8, size / 8, 1);
    }

    void SpectrumUpdate(float time)
    {
        shaderSpectrum.SetFloat("time", time);

        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "inputH0", bufferSpectrumH0);
        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "outputH", bufferSpectrumH);
        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "outputDx", bufferSpectrumDx);
        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "outputDy", bufferSpectrumDy);

        shaderSpectrum.Dispatch(kernelSpectrumUpdate, size / 8, size / 8, 1);
    }

    void FFT(RenderTexture spectrum, RenderTexture output)
    {
        shaderFFT.SetTexture(kernelFFTX, "input", spectrum);
        shaderFFT.SetTexture(kernelFFTX, "inputButterfly", texButterfly);
        shaderFFT.SetTexture(kernelFFTX, "output", bufferFFTTemp);
        shaderFFT.Dispatch(kernelFFTX, 1, size, 1);
        shaderFFT.SetTexture(kernelFFTY, "input", bufferFFTTemp);
        shaderFFT.SetTexture(kernelFFTY, "inputButterfly", texButterfly);
        shaderFFT.SetTexture(kernelFFTY, "output", output);
        shaderFFT.Dispatch(kernelFFTY, size, 1, 1);
    }

    void Combine()
    {
        materialCombine.SetInt("size", size);
        materialCombine.SetFloat("domainSize", domainSize);
        materialCombine.SetFloat("invDomainSize", 1.0f / domainSize);
        materialCombine.SetFloat("choppiness", choppiness);

        materialCombine.SetTexture("inputH", bufferHFinal);
        materialCombine.SetTexture("inputDx", bufferDxFinal);
        materialCombine.SetTexture("inputDy", bufferDyFinal);
        
        Graphics.SetRenderTarget(new RenderBuffer[] { bufferDisplacement.colorBuffer, bufferGradientFold.colorBuffer }, bufferDisplacement.depthBuffer);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, size, size, 0);
        GL.Viewport(new Rect(0, 0, size, size));
        materialCombine.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.TexCoord2(0, 0);
        GL.Vertex3(0, 0, 0);
        GL.TexCoord2(1, 0);
        GL.Vertex3(size, 0, 0);
        GL.TexCoord2(1, 1);
        GL.Vertex3(size, size, 0);
        GL.TexCoord2(0, 1);
        GL.Vertex3(0, size, 0);
        GL.End();
        GL.PopMatrix();
        Graphics.SetRenderTarget(null);
    }

    Vector3 GetPlaneBase(Vector3 n, int index)
    {
        if (index == 1)
        {
            if (n.x == 0.0f)
            {
                return Vector3.right;
            }
            else if (n.y == 0.0f)
            {
                return Vector3.up;
            }
            else if (n.z == 0.0f)
            {
                return Vector3.forward;
            }
            return new Vector3(-n.y, n.x, 0.0f);
        }
        return Vector3.Cross(n, GetPlaneBase(n, 1));
    }

    Vector2 To2D(Vector3 n, Vector3 p)
    {
        var v1 = GetPlaneBase(n, 1);
        var v2 = GetPlaneBase(n, 2);
        var v3 = n;

        float denom = v2.y * v3.x * v1.z - v2.x * v3.y * v1.z + v3.z * v2.x * v1.y +
               v2.z * v3.y * v1.x - v3.x * v2.z * v1.y - v2.y * v3.z * v1.x;
        float x = -(v2.y * v3.z * p.x - v2.y * v3.x * p.z + v3.x * v2.z * p.y +
                  v2.x * v3.y * p.z - v3.z * v2.x * p.y - v2.z * v3.y * p.x) / denom;
        float y = (v1.y * v3.z * p.x - v1.y * v3.x * p.z - v3.y * p.x * v1.z +
                v3.y * v1.x * p.z + p.y * v3.x * v1.z - p.y * v3.z * v1.x) / denom;

        return new Vector2(x, y);
    }

    Vector3? GetIntersection(Vector3 planeOrigin, Vector3 planeNormal, Vector3 p0, Vector3 p1)
    {
        float den = Vector3.Dot(planeNormal, p1 - p0);
        if (Mathf.Abs(den) < float.Epsilon)
        {
            return null;
        }
        float u = Vector3.Dot(planeNormal, planeOrigin - p0) / den;
        if (u < 0.0f || u > 1.0f)
        {
            return null;
        }
        return p0 + u * (p1 - p0);
    }

    void AddPoint(List<Vector3> points, Vector3? point)
    {
        if (point.HasValue)
        {
            points.Add(point.Value);
        }
    }

    void DrawDebug(Vector3 point, Color color)
    {
        float size = 0.4f;
        Debug.DrawLine(point - new Vector3(size, 0.0f, 0.0f), point + new Vector3(size, 0.0f, 0.0f), color);
        Debug.DrawLine(point - new Vector3(0.0f, size, 0.0f), point + new Vector3(0.0f, size, 0.0f), color);
        Debug.DrawLine(point - new Vector3(0.0f, 0.0f, size), point + new Vector3(0.0f, 0.0f, size), color);
    }

    void ComputeExtendedPlane()
    {
        var camera = GetComponent<Camera>();
        var nearTL = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));
        var nearTR = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
        var nearBL = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
        var nearBR = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
        var farTL = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.farClipPlane));
        var farTR = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.farClipPlane));
        var farBL = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.farClipPlane));
        var farBR = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.farClipPlane));

        //Debug.DrawLine(nearTL, farTL);
        //Debug.DrawLine(nearTR, farTR);
        //Debug.DrawLine(nearBL, farBL);
        //Debug.DrawLine(nearBR, farBR);

        var planeOrigin = new Vector3(camera.transform.position.x, 0.0f, camera.transform.position.z);
        var planeNormal = Vector3.up;

        var points = new List<Vector3>();
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearTL, farTL));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearTR, farTR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearBL, farBL));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearBR, farBR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, farTL, farTR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, farBL, farBR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, farTL, farBL));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, farTR, farBR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearTL, nearTR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearBL, nearBR));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearTL, nearBL));
        AddPoint(points, GetIntersection(planeOrigin, planeNormal, nearTR, nearBR));
        if (points.Count == 0)
        {
            return;
        }

        var center = Vector2.zero;
        var points2D = new List<Vector2>();
        foreach (var p in points)
        {
            var p2D = To2D(planeNormal, p);
            center += p2D;
            points2D.Add(p2D);
        }
        center /= points.Count;

        var v1 = GetPlaneBase(planeNormal, 1);
        var v2 = GetPlaneBase(planeNormal, 2);
        DrawDebug(v1 * center.x + v2 * center.y, Color.blue);

        Func<Vector2, Vector2, bool> less = (Vector2 a, Vector2 b) =>
        {
            if (a.x - center.x >= 0 && b.x - center.x < 0)
                return true;
            if (a.x - center.x < 0 && b.x - center.x >= 0)
                return false;
            if (a.x - center.x == 0 && b.x - center.x == 0)
            {
                if (a.y - center.y >= 0 || b.y - center.y >= 0)
                    return a.y > b.y;
                return b.y > a.y;
            }

            // compute the cross product of vectors (center -> a) x (center -> b)
            float det = (a.x - center.x) * (b.y - center.y) - (b.x - center.x) * (a.y - center.y);
            if (det < 0)
                return true;
            if (det > 0)
                return false;

            // points a and b are on the same line from the center
            // check which point is closer to the center
            float d1 = (a.x - center.x) * (a.x - center.x) + (a.y - center.y) * (a.y - center.y);
            float d2 = (b.x - center.x) * (b.x - center.x) + (b.y - center.y) * (b.y - center.y);
            return d1 > d2;
        };

        points2D.Sort((Vector2 a, Vector2 b) =>
        {
            return less(a, b) ? -1 : 1;
        });

        var points3D = new List<Vector3>();
        points3D.Add(v1 * center.x + v2 * center.y);
        var indices = new List<int>();
        for (int i = 0; i < points2D.Count; i++)
        {
            var p3D = v1 * points2D[i].x + v2 * points2D[i].y;
            points3D.Add(p3D);
            indices.Add(0);
            indices.Add(1 + (i + 1) % points2D.Count);
            indices.Add(1 + i);
            var p3Dnext = v1 * points2D[(i + 1) % points2D.Count].x + v2 * points2D[(i+1)%points2D.Count].y;
            Debug.DrawLine(p3D, p3Dnext, Color.blue);
        }

        var plane = new Mesh();
        plane.vertices = points3D.ToArray();
        plane.triangles = indices.ToArray();
        plane.UploadMeshData(false);
        plane.RecalculateNormals();

        Graphics.DrawMesh(plane, Matrix4x4.identity, materialExtended, gameObject.layer);
    }

    void OnPreRender()
    {
        GL.wireframe = wireframe;
    }

    void OnPostRender()
    {
        GL.wireframe = false;
    }

    void Update()
    {
        SpectrumInit();
        SpectrumUpdate(Time.time);
        FFT(bufferSpectrumH, bufferHFinal);
        FFT(bufferSpectrumDx, bufferDxFinal);
        FFT(bufferSpectrumDy, bufferDyFinal);
        Combine();
    }

    void LateUpdate()
    {
        var worldPosition = transform.position;
        var spacing = domainSize / meshSize;
        var snappedPositionX = spacing * Mathf.FloorToInt(worldPosition.x / spacing);
        var snappedPositionY = spacing * Mathf.FloorToInt(worldPosition.z / spacing);
        var matrix = Matrix4x4.TRS(new Vector3(snappedPositionX, 0.0f, snappedPositionY), Quaternion.identity, new Vector3(domainSize, 1, domainSize));

        var snappedUVPosition = new Vector4(snappedPositionX - domainSize * 0.5f, 0, snappedPositionY - domainSize * 0.5f, 1) / domainSize;

        material.SetTexture("_DispTex", bufferDisplacement);
        material.SetTexture("_NormalMap", bufferGradientFold);
        material.SetFloat("_Choppiness", choppiness);
        material.SetFloat("_NormalTexelSize", 2.0f * domainSize / size);
        material.SetVector("_SnappedWorldPosition", snappedUVPosition);
        Graphics.DrawMesh(mesh, matrix, material, gameObject.layer);

        materialExtended.SetTexture("_DispTex", bufferDisplacement);
        materialExtended.SetTexture("_NormalMap", bufferGradientFold);
        materialExtended.SetFloat("_Choppiness", choppiness);
        materialExtended.SetFloat("_InvDomainSize", 1.0f / domainSize);
        materialExtended.SetFloat("_NormalTexelSize", 2.0f * domainSize / size);
        ComputeExtendedPlane();
    }

    void OnGUI()
    {
        if (debug)
        {
            GUI.DrawTexture(new Rect(0, 0, size * 2, size * 2), bufferDisplacement, ScaleMode.ScaleToFit, false);
        }
    }
}
