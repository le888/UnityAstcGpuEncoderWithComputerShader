using System.Collections;
using System.Collections.Generic;
using GPUASTCTextureCompressor;
using UnityEngine;

public class ToolsTest : MonoBehaviour
{
    public Material mt;

    public Texture image;
    // Start is called before the first frame update
    void Start()
    {
        var t = GPUTextureCompressorTools.Instance.CompressTexture(image);
        mt.mainTexture = t;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
