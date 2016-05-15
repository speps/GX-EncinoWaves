﻿using UnityEngine;
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

    private ComputeShader shaderCombine;

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
        shaderCombine = (ComputeShader)Resources.Load("EncinoCombine", typeof(ComputeShader));

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
        shaderCombine.SetInt("size", size);
        shaderCombine.SetFloat("domainSize", domainSize);
        shaderCombine.SetFloat("invDomainSize", 1.0f / domainSize);
        shaderCombine.SetFloat("choppiness", choppiness);

        shaderCombine.SetTexture(0, "inputH", bufferHFinal);
        shaderCombine.SetTexture(0, "inputDx", bufferDxFinal);
        shaderCombine.SetTexture(0, "inputDy", bufferDyFinal);

        shaderCombine.SetTexture(0, "outputDisplacement", bufferDisplacement);
        shaderCombine.SetTexture(0, "outputGradientFold", bufferGradientFold);

        shaderCombine.Dispatch(0, size / 8, size / 8, 1);
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

    void DrawDebug(Vector3? point, Color color)
    {
        if (point.HasValue)
        {
            float size = 0.4f;
            Debug.DrawLine(point.Value - new Vector3(size, 0.0f, 0.0f), point.Value + new Vector3(size, 0.0f, 0.0f), color);
            Debug.DrawLine(point.Value - new Vector3(0.0f, size, 0.0f), point.Value + new Vector3(0.0f, size, 0.0f), color);
            Debug.DrawLine(point.Value - new Vector3(0.0f, 0.0f, size), point.Value + new Vector3(0.0f, 0.0f, size), color);
        }
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

        Debug.DrawLine(nearTL, farTL);
        Debug.DrawLine(nearTR, farTR);
        Debug.DrawLine(nearBL, farBL);
        Debug.DrawLine(nearBR, farBR);

        var planeOrigin = new Vector3(camera.transform.position.x, 0.0f, camera.transform.position.z);
        var planeNormal = Vector3.up;

        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearTL, farTL), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearTR, farTR), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearBL, farBL), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearBR, farBR), Color.yellow);

        DrawDebug(GetIntersection(planeOrigin, planeNormal, farTL, farTR), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, farBL, farBR), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, farTL, farBL), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, farTR, farBR), Color.yellow);

        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearTL, nearTR), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearBL, nearBR), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearTL, nearBL), Color.yellow);
        DrawDebug(GetIntersection(planeOrigin, planeNormal, nearTR, nearBR), Color.yellow);
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

        material.SetTexture("_DispTex", bufferDisplacement);
        material.SetTexture("_NormalMap", bufferGradientFold);
        material.SetFloat("_Choppiness", choppiness);
        material.SetFloat("_NormalTexelSize", 2.0f * domainSize / size);
        material.SetVector("_SnappedWorldPosition", new Vector4(snappedPositionX, 0, snappedPositionY, 1) / domainSize);
        Graphics.DrawMesh(mesh, matrix, material, gameObject.layer);

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
