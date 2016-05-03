using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System;

//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class EncinoWaves : MonoBehaviour
{
    private ComputeShader shaderSpectrum;
    private int kernelSpectrumInit;
    private int kernelSpectrumUpdate;

    private ComputeShader shaderFFT;
    private int kernelFFTX = 0;
    private int kernelFFTY = 1;

    private int size = 256;
    private int meshSize = 32;
    private float domainSize = 100.0f;
    // Initial spectrum
    private RenderTexture bufferSpectrumH0;
    private RenderTexture bufferSpectrumOmega;
    // Updated spectrum
    private RenderTexture bufferSpectrumH;
    // Final height
    private RenderTexture bufferHTemp;
    private RenderTexture bufferHFinal;
    private RenderTexture bufferHCopy;
    private Texture2D texButterfly;

    public Mesh mesh;
    public Material material;
    public bool wireframe;

    void OnEnable()
    {
        shaderSpectrum = (ComputeShader)Resources.Load("EncinoSpectrum", typeof(ComputeShader));
        kernelSpectrumInit = shaderSpectrum.FindKernel("EncinoSpectrumInit");
        kernelSpectrumUpdate = shaderSpectrum.FindKernel("EncinoSpectrumUpdate");
        shaderFFT = (ComputeShader)Resources.Load("EncinoFFT", typeof(ComputeShader));

        bufferSpectrumH0 = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat);
        bufferSpectrumH0.enableRandomWrite = true;
        bufferSpectrumH0.Create();
        bufferSpectrumOmega = new RenderTexture(size, size, 0, RenderTextureFormat.RFloat);
        bufferSpectrumOmega.enableRandomWrite = true;
        bufferSpectrumOmega.Create();
        bufferSpectrumH = new RenderTexture(size, size, 0, RenderTextureFormat.RGFloat);
        bufferSpectrumH.enableRandomWrite = true;
        bufferSpectrumH.Create();
        bufferHTemp = new RenderTexture(size, size, 0, RenderTextureFormat.RGFloat);
        bufferHTemp.enableRandomWrite = true;
        bufferHTemp.Create();
        bufferHFinal = new RenderTexture(size, size, 0, RenderTextureFormat.RFloat);
        bufferHFinal.enableRandomWrite = true;
        bufferHFinal.Create();
        bufferHCopy = new RenderTexture(size, size, 0, RenderTextureFormat.RFloat);
        bufferHCopy.generateMips = true;
        bufferHCopy.filterMode = FilterMode.Bilinear;
        bufferHCopy.wrapMode = TextureWrapMode.Repeat;
        bufferHCopy.Create();

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
        shaderSpectrum.SetTexture(kernelSpectrumInit, "outputOmega", bufferSpectrumOmega);

        shaderSpectrum.Dispatch(kernelSpectrumInit, size / 8, size / 8, 1);
    }

    void SpectrumUpdate(float time)
    {
        shaderSpectrum.SetFloat("time", time);

        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "inputH0", bufferSpectrumH0);
        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "inputOmega", bufferSpectrumOmega);
        shaderSpectrum.SetTexture(kernelSpectrumUpdate, "outputH", bufferSpectrumH);

        shaderSpectrum.Dispatch(kernelSpectrumUpdate, size / 8, size / 8, 1);
    }

    void FFT()
    {
        shaderFFT.SetTexture(kernelFFTX, "input", bufferSpectrumH);
        shaderFFT.SetTexture(kernelFFTX, "inputButterfly", texButterfly);
        shaderFFT.SetTexture(kernelFFTX, "output", bufferHTemp);
        shaderFFT.Dispatch(kernelFFTX, 1, size, 1);
        shaderFFT.SetTexture(kernelFFTY, "input", bufferHTemp);
        shaderFFT.SetTexture(kernelFFTY, "inputButterfly", texButterfly);
        shaderFFT.SetTexture(kernelFFTY, "output", bufferHFinal);
        shaderFFT.Dispatch(kernelFFTY, size, 1, 1);

        Graphics.Blit(bufferHFinal, bufferHCopy);
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
        FFT();
    }

    void LateUpdate()
    {
        var worldPosition = transform.position;
        var spacing = domainSize / meshSize;
        var snappedPositionX = spacing * Mathf.FloorToInt(worldPosition.x / spacing);
        var snappedPositionY = spacing * Mathf.FloorToInt(worldPosition.z / spacing);
        var matrix = Matrix4x4.TRS(new Vector3(snappedPositionX, 0.0f, snappedPositionY), Quaternion.identity, new Vector3(domainSize, 1, domainSize));

        material.SetTexture("_DispTex", bufferHCopy);
        material.SetFloat("_DomainSize", domainSize);
        material.SetVector("_SnappedWorldPosition", new Vector4(snappedPositionX, 0, snappedPositionY, 1) / domainSize);
        Graphics.DrawMesh(mesh, matrix, material, 0);
    }
}
