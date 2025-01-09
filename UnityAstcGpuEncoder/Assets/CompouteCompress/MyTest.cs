using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;


namespace MyTest
{
    public enum ASTC_BLOCKSIZE
    {
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
    }

    public class MyTest : MonoBehaviour
    {
        public Texture sourceTexture;
        
        private static int k_ResultId = Shader.PropertyToID("_Result");
        private static int k_DestRectId = Shader.PropertyToID("_DestRect");
        private static readonly int k_IntegerFromQuintsId = Shader.PropertyToID("integer_from_quints");
        private static readonly int k_ColorQuantTableId = Shader.PropertyToID("color_quant_table");
        
        private static int k_SourceTextureId = Shader.PropertyToID("_CompressSourceTexture");
        private static int k_SourceTextureMipLevelId = Shader.PropertyToID("_CompressSourceTexture_MipLevel");
        
        private RenderTexture m_DecompressTexture;


        public ComputeShader computeShader;

        public Material material;
        
        // Start is called before the first frame update
        RenderTexture astcRT;

        private Texture2D deTxture;
        
        public ASTC_BLOCKSIZE astcTYPE = ASTC_BLOCKSIZE.ASTC_5x5;
        
        int imageSizeWidth; 
        int imageSizeHeight;
        
        private void OnEnable()
        {
            int blockSize = CompressBlockSize;
            imageSizeWidth = Mathf.CeilToInt(sourceTexture.width / (float)blockSize);
            imageSizeHeight = Mathf.CeilToInt(sourceTexture.height / (float)blockSize);
            // imageSizeWidth = (int)(sourceTexture.width / (float)blockSize);
            // imageSizeHeight = (int)(sourceTexture.height / (float)blockSize);
            astcRT = new RenderTexture(imageSizeWidth, imageSizeHeight, 0,GraphicsFormat.R32G32B32A32_UInt,1);
            astcRT.enableRandomWrite = true;
            astcRT.name = "astcRT Intermediate texture";
            astcRT.Create();
            
            var format  = astcTYPE == ASTC_BLOCKSIZE.ASTC_4x4 ? TextureFormat.ASTC_4x4 :
                astcTYPE == ASTC_BLOCKSIZE.ASTC_5x5 ? TextureFormat.ASTC_5x5 : TextureFormat.ASTC_6x6;
            
            GraphicsFormat gfxFormat = GraphicsFormatUtility.GetGraphicsFormat(format, true);
            if (DecompressAstc())
                gfxFormat = GraphicsFormat.R8G8B8A8_UNorm;
            
            deTxture = new Texture2D(imageSizeWidth * blockSize, imageSizeHeight * blockSize,gfxFormat,1, TextureCreationFlags.None);
            // deTxture = new Texture2D(sourceTexture.width, sourceTexture.height,gfxFormat,0, TextureCreationFlags.None);
            deTxture.filterMode = FilterMode.Trilinear;
            deTxture.wrapMode = TextureWrapMode.Clamp;
            deTxture.name = "aaaaaaa astc Destexture";//方便内存查看
            deTxture.Apply(false, true); // 让贴图变成不可读，以卸载内存只保留显存
            
            
            if (DecompressAstc())
            {
                if (m_DecompressTexture)
                    DestroyImmediate(m_DecompressTexture);
                
                // m_DecompressTexture = new RenderTexture(imageSizeWidth * blockSize, imageSizeHeight * blockSize, 0, GraphicsFormat.R8G8B8A8_UNorm, 1);
                m_DecompressTexture = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, GraphicsFormat.R8G8B8A8_UNorm, 1);
                m_DecompressTexture.hideFlags = HideFlags.HideAndDontSave;
                m_DecompressTexture.name = "GPU Compressor Decompress Texture";
                m_DecompressTexture.enableRandomWrite = true;
                m_DecompressTexture.Create();
            }
        }
        
        

        private void OnDisable()
        {
            astcRT?.Release();
            m_DecompressTexture?.Release();
            m_DecompressTexture = null;
        }

        // Update is called once per frame
        void Update()
        {
            if (computeShader == null)
            {
                return;
            }

           
            RecreateMaterial(computeShader, astcTYPE, astcTYPE);
            
            int kernelHandle = computeShader.FindKernel("CSCompress");
            
            if (DecompressAstc())
                computeShader.SetTexture(kernelHandle, "_ResultDecompressed", m_DecompressTexture);
                
            
            int blockSize = CompressBlockSize;
            computeShader.SetTexture(kernelHandle,k_SourceTextureId,sourceTexture);
            computeShader.SetVector(k_DestRectId, new Vector4(sourceTexture.width, sourceTexture.height, 1.0f / sourceTexture.width, 1.0f / sourceTexture.height));
            computeShader.SetInt(k_SourceTextureMipLevelId, 0);
            computeShader.SetTexture(kernelHandle, k_ResultId, astcRT);
            computeShader.SetVector("TextureSize", new Vector4(sourceTexture.width, sourceTexture.height, 0, 0));
            computeShader.Dispatch(kernelHandle, sourceTexture.width / blockSize, sourceTexture.height / blockSize, 1);
            
            //复制astcRT到deTxture
            // Graphics.CopyTexture(astcRT,0,0,0,0,astcRT.width,astcRT.height,deTxture,0,0,0,0);
            if (!DecompressAstc())
            {
                Graphics.CopyTexture(astcRT,0,0,0,0,astcRT.width,astcRT.height,deTxture,0,0,0,0);
            }
            else
            {
                Graphics.CopyTexture(m_DecompressTexture,0,0,0,0,m_DecompressTexture.width,m_DecompressTexture.height,deTxture,0,0,0,0);    
            }

            
            // Graphics.ConvertTexture(astcRT, deTxture);
            if (material != null)
            {
                material.mainTexture = deTxture;
            }
        }

        private void RecreateMaterial(ComputeShader compressShader, ASTC_BLOCKSIZE prevBlocksize,
            ASTC_BLOCKSIZE blocksize)
        {
            // if (compressShader != null && compressShader.shader == compressShader)
            // {
            //     if (prevBlocksize != blocksize)
            //     {
            //         // 仍然需要重新创建材质
            //         DestroyImmediate(m_CompressMaterial);
            //         m_CompressMaterial = null;
            //     }
            //     else
            //     {
            //         return;
            //     }
            // }

            // m_CompressMaterial = new Material(compressShader);
            // m_CompressMaterial.hideFlags = HideFlags.HideAndDontSave;

            if (blocksize == ASTC_BLOCKSIZE.ASTC_5x5)
            {
                compressShader.EnableKeyword("_COMPRESS_ASTC5x5");
                compressShader.DisableKeyword("_COMPRESS_ASTC4x4");
                compressShader.DisableKeyword("_COMPRESS_ASTC6x6");
            }
            else if (blocksize == ASTC_BLOCKSIZE.ASTC_4x4)
            {
                compressShader.EnableKeyword("_COMPRESS_ASTC4x4");
                compressShader.DisableKeyword("_COMPRESS_ASTC5x5");
                compressShader.DisableKeyword("_COMPRESS_ASTC6x6");
            }
            else
            {
                compressShader.EnableKeyword("_COMPRESS_ASTC6x6");
                compressShader.DisableKeyword("_COMPRESS_ASTC4x4");
                compressShader.DisableKeyword("_COMPRESS_ASTC5x5");

                var quintsLookup = new float[]
                {
                    0, 1, 2, 3, 4, 8, 9, 10, 11, 12, 16, 17, 18, 19, 20, 24, 25, 26, 27, 28, 5, 13, 21, 29, 6,
                    32, 33, 34, 35, 36, 40, 41, 42, 43, 44, 48, 49, 50, 51, 52, 56, 57, 58, 59, 60, 37, 45, 53, 61, 14,
                    64, 65, 66, 67, 68, 72, 73, 74, 75, 76, 80, 81, 82, 83, 84, 88, 89, 90, 91, 92, 69, 77, 85, 93, 22,
                    96, 97, 98, 99, 100, 104, 105, 106, 107, 108, 112, 113, 114, 115, 116, 120, 121, 122, 123, 124, 101,
                    109, 117, 125, 30,
                    102, 103, 70, 71, 38, 110, 111, 78, 79, 46, 118, 119, 86, 87, 54, 126, 127, 94, 95, 62, 39, 47, 55,
                    63, 31
                };
                for (int i = 0; i < quintsLookup.Length; i++)
                    quintsLookup[i] += 0.5f;

                var colorQuantTable = new float[]
                {
                    0, 0, 16, 16, 16, 32, 32, 32, 48, 48, 48, 48, 64, 64, 64, 2,
                    2, 2, 18, 18, 18, 34, 34, 34, 50, 50, 50, 50, 66, 66, 66, 4,
                    4, 4, 20, 20, 20, 36, 36, 36, 36, 52, 52, 52, 68, 68, 68, 6,
                    6, 6, 22, 22, 22, 38, 38, 38, 38, 54, 54, 54, 70, 70, 70, 8,
                    8, 8, 24, 24, 24, 24, 40, 40, 40, 56, 56, 56, 72, 72, 72, 10,
                    10, 10, 26, 26, 26, 26, 42, 42, 42, 58, 58, 58, 74, 74, 74, 12,
                    12, 12, 12, 28, 28, 28, 44, 44, 44, 60, 60, 60, 76, 76, 76, 14,
                    14, 14, 14, 30, 30, 30, 46, 46, 46, 62, 62, 62, 78, 78, 78, 78,
                    79, 79, 79, 79, 63, 63, 63, 47, 47, 47, 31, 31, 31, 15, 15, 15,
                    15, 77, 77, 77, 61, 61, 61, 45, 45, 45, 29, 29, 29, 13, 13, 13,
                    13, 75, 75, 75, 59, 59, 59, 43, 43, 43, 27, 27, 27, 27, 11, 11,
                    11, 73, 73, 73, 57, 57, 57, 41, 41, 41, 25, 25, 25, 25, 9, 9,
                    9, 71, 71, 71, 55, 55, 55, 39, 39, 39, 39, 23, 23, 23, 7, 7,
                    7, 69, 69, 69, 53, 53, 53, 37, 37, 37, 37, 21, 21, 21, 5, 5,
                    5, 67, 67, 67, 51, 51, 51, 51, 35, 35, 35, 19, 19, 19, 3, 3,
                    3, 65, 65, 65, 49, 49, 49, 49, 33, 33, 33, 17, 17, 17, 1, 1,
                };
                for (int i = 0; i < colorQuantTable.Length; i++)
                    colorQuantTable[i] += 0.5f;

                compressShader.SetFloats(k_IntegerFromQuintsId, quintsLookup);
                compressShader.SetFloats(k_ColorQuantTableId, colorQuantTable);
            }

            if (DecompressAstc())
                compressShader.EnableKeyword("_DECOMPRESS_RGB");
            else
                compressShader.DisableKeyword("_DECOMPRESS_RGB");
        }

        public static bool DecompressAstc()
        {
#if UNITY_EDITOR
            return s_DecompressInEditor;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        // 在编辑器中仍然使用ASTC压缩。由于PC平台不支持ASTC，所以需要在压缩的同时会将压缩结果解压到uav中
        // 这个选项的目的是方便在编辑器中预览压缩后的效果
        private static bool s_DecompressInEditor = true;
#endif
       
        private int CompressBlockSize
        {
            get
            {
                switch (astcTYPE)
                {
                    case ASTC_BLOCKSIZE.ASTC_4x4: return 4;
                    case ASTC_BLOCKSIZE.ASTC_5x5: return 5;
                    case ASTC_BLOCKSIZE.ASTC_6x6: return 6;
                    default: throw new System.ArgumentException("Invalid ASTC block size");
                }                    
            }
        }
        
        
    }
    

}