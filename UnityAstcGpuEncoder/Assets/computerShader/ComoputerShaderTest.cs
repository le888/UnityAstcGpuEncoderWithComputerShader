using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class ComoputerShaderTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public Material material;
    // Start is called before the first frame update
    int imageSize = 2048;
    RenderTexture tex;
    private void OnEnable()
    {
        tex = new RenderTexture(imageSize, imageSize, 0);
        tex.enableRandomWrite = true;
        tex.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        tex.Create();
    }

    private void OnDisable()
    {
        tex?.Release();
    }

    // Update is called once per frame
    void Update()
    {
        if (computeShader == null)
        {
            return;
        }
        
        int kernelHandle = computeShader.FindKernel("CSMain");
        
        computeShader.SetTexture(kernelHandle, "Result", tex);
        computeShader.SetFloat("imageSize", imageSize);
        computeShader.Dispatch(kernelHandle, imageSize / 8, imageSize / 8, 1);

        if (material != null)
        {
            material.mainTexture = tex;            
        }
    }
}
