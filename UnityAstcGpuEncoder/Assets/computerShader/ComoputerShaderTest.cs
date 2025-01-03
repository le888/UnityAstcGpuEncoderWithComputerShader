using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComoputerShaderTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public Material material;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (computeShader == null)
        {
            return;
        }
        
        int kernelHandle = computeShader.FindKernel("CSMain");
        RenderTexture tex = new RenderTexture(256, 256, 24);
        tex.enableRandomWrite = true;
        tex.Create();
        computeShader.SetTexture(kernelHandle, "Result", tex);
        computeShader.Dispatch(kernelHandle, 256 / 8, 256 / 8, 1);

        if (material != null)
        {
            material.mainTexture = tex;            
        }
    }
}
