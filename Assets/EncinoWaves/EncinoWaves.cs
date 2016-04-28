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
    private int meshSize = 128;
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
        bufferHCopy.Create();

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

        {
            mesh = new Mesh();

            float spacing = domainSize / meshSize;
            float offset = -0.5f * domainSize;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int y = 0; y < meshSize; y++)
            {
                for (int x = 0; x < meshSize; x++)
                {
                    vertices.Add(new Vector3(offset + x * spacing, 0.0f, offset + y * spacing));
                    uvs.Add(new Vector2((float)x / (meshSize - 1), (float)y / (meshSize - 1)));
                }
            }

            var triangles = new List<int>();
            for (int y = 0; y < (meshSize - 1); y++)
            {
                for (int x = 0; x < (meshSize - 1); x++)
                {
                    var i0 = y * meshSize + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + meshSize;
                    var i3 = i2 + 1;

                    triangles.Add(i0);
                    triangles.Add(i1);
                    triangles.Add(i2);

                    triangles.Add(i1);
                    triangles.Add(i3);
                    triangles.Add(i2);
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

    void Update()
    {
        SpectrumInit();
        SpectrumUpdate(Time.time);
        FFT();
    }

    void LateUpdate()
    {
        material.SetTexture("_HeightTex", bufferHCopy);
        Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, material, 0);
    }

    void OnGUI()
    {
        //GUI.DrawTexture(new Rect(0, 0, size, size), bufferSpectrumH0, ScaleMode.ScaleToFit, false);
        //GUI.DrawTexture(new Rect(0, size + 1, size, size), bufferSpectrumH, ScaleMode.ScaleToFit, false);
        GUI.DrawTexture(new Rect(size + 1, 0, size, size), bufferHCopy, ScaleMode.ScaleToFit, false);
        //GUI.DrawTexture(new Rect(size + 1, size + 1, size, size), texButterfly, ScaleMode.ScaleToFit, false);
    }
}
