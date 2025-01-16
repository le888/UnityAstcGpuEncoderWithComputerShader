using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace GPUASTCTextureCompressor
{
    public enum ASTC_BLOCKSIZE
    {
        ASTC_4x4,
        ASTC_5x5,
        ASTC_6x6,
    }

    public class GPUTextureCompressorTools : MonoBehaviour
    {
        public ASTC_BLOCKSIZE astcTYPE = ASTC_BLOCKSIZE.ASTC_4x4; //默认4x4，方便被2048 等整除，非整除 边缘部分会有问题
        public ComputeShader astcCompressorComputeShader; //compute shader 压缩
        public Shader astcCompressorShader; //fragment shader 压缩
        private ComputerShaderCompressor _computerShaderCompressor;
        private FragmentShaderCompressor _fragmentShaderCompressor;

        //一个单列
        private static GPUTextureCompressorTools _instance;

        private void Awake()
        {
            _instance = this;
        }

        public static GPUTextureCompressorTools Instance
        {
            get
            {
                // if (_instance == null)
                // {
                //     _instance = new GPUTextureCompressorTools();
                // }

                return _instance;
            }
        }


        private RenderTexture astcIntermediateTexture; //做一下缓冲，不用每次都创建，大小对不上时才创建

        //压缩纹理
        public Texture2D CompressTexture(Texture sourceTexture,
            GraphicsFormat graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, bool isNormalMap = false)
        {
            int blockSize = CompressBlockSize;
            int astcIntermediateSizeWidth = Mathf.CeilToInt(sourceTexture.width / (float)blockSize);
            int astcIntermediateSizeHeight = Mathf.CeilToInt(sourceTexture.height / (float)blockSize);

            //检查上次创建的大小是否匹配，不匹配则重新创建
            CheckIntermediateTexture(astcIntermediateSizeWidth, astcIntermediateSizeHeight);
            CreateIntermediateTexture(astcIntermediateSizeWidth, astcIntermediateSizeHeight);
            //创建压缩后的目标纹理
            Texture2D targetTexture = CreateTargetTexture(sourceTexture, astcIntermediateSizeWidth,
                astcIntermediateSizeHeight, blockSize);

            RenderTexture m_DecompressTexture = null;
            if (DecompressAstc()) //编辑器下处理
            {
                m_DecompressTexture =
                    new RenderTexture(sourceTexture.width, sourceTexture.height, 0, graphicsFormat, 1);
                m_DecompressTexture.hideFlags = HideFlags.HideAndDontSave;
                m_DecompressTexture.name = sourceTexture.name + "GPU Compressor Decompress Texture";
                m_DecompressTexture.enableRandomWrite = true;
                m_DecompressTexture.Create();
            }

            //压缩
            CompressorTexture(sourceTexture, m_DecompressTexture);

            //给targetTexture赋值
            if (!DecompressAstc())
            {
                Graphics.CopyTexture(astcIntermediateTexture, 0, 0, 0, 0, astcIntermediateTexture.width,
                    astcIntermediateTexture.height, targetTexture, 0, 0, 0, 0);
            }
            else
            {
                Graphics.CopyTexture(m_DecompressTexture, 0, 0, 0, 0, m_DecompressTexture.width,
                    m_DecompressTexture.height, targetTexture, 0, 0, 0, 0);

                //释放m_DecompressTexture
                m_DecompressTexture.Release();
            }


            //释放sourceTexture
            if (sourceTexture is RenderTexture)
            {
                (sourceTexture as RenderTexture).Release();
            }

            return targetTexture;
        }

        private void CompressorTexture(Texture sourceTexture, RenderTexture m_DecompressTexture)
        {
            //检查是否支持computer shader
            if (SystemInfo.supportsComputeShaders)
            {
                //compute shader 压缩
                if (_computerShaderCompressor == null)
                    _computerShaderCompressor = new ComputerShaderCompressor(astcCompressorComputeShader);

                _computerShaderCompressor.Compressor(astcTYPE, m_DecompressTexture, sourceTexture, astcIntermediateTexture);
            }
            else
            {
                //fragment shader 压缩
                if (_fragmentShaderCompressor == null)
                    _fragmentShaderCompressor = new FragmentShaderCompressor(astcCompressorShader);
                
                _fragmentShaderCompressor.Compressor(astcTYPE, m_DecompressTexture, sourceTexture,astcIntermediateTexture ,true);
            }
        }

        // private void ComputerShaderCompressor()
        // {
        //     if (astcCompressorComputeShader == null)
        //     {
        //         Debug.LogError("astcCompressorComputeShader is null");
        //         return;
        //     }
        // }

        private Texture2D CreateTargetTexture(Texture sourceTexture, int astcIntermediateSizeWidth,
            int astcIntermediateSizeHeight,
            int blockSize)
        {
            var format = astcTYPE == ASTC_BLOCKSIZE.ASTC_4x4 ? TextureFormat.ASTC_4x4 :
                astcTYPE == ASTC_BLOCKSIZE.ASTC_5x5 ? TextureFormat.ASTC_5x5 : TextureFormat.ASTC_6x6;
            GraphicsFormat gfxFormat = GraphicsFormatUtility.GetGraphicsFormat(format, true);
            if (DecompressAstc())
                gfxFormat = GraphicsFormat.R8G8B8A8_UNorm;
            Texture2D targetTexture = new Texture2D(astcIntermediateSizeWidth * blockSize,
                astcIntermediateSizeHeight * blockSize, gfxFormat, 1, TextureCreationFlags.None);
            targetTexture.filterMode = FilterMode.Bilinear;
            targetTexture.wrapMode = TextureWrapMode.Clamp;
            targetTexture.name = sourceTexture.name + "_astc"; //方便内存查看
            targetTexture.Apply(false, true); // 让贴图变成不可读，以卸载内存只保留显存
            return targetTexture;
        }

        private void CreateIntermediateTexture(int astcIntermediateSizeWidth, int astcIntermediateSizeHeight)
        {
            if (astcIntermediateTexture == null)
            {
                astcIntermediateTexture = new RenderTexture(astcIntermediateSizeWidth, astcIntermediateSizeHeight, 0,
                    GraphicsFormat.R32G32B32A32_UInt, 1);
                astcIntermediateTexture.enableRandomWrite = true;
                astcIntermediateTexture.name = "astcRT Intermediate texture";
                astcIntermediateTexture.Create();
            }
        }

        private void CheckIntermediateTexture(int astcIntermediateSizeWidth, int astcIntermediateSizeHeight)
        {
            if (astcIntermediateTexture != null)
            {
                if (astcIntermediateTexture.width != astcIntermediateSizeWidth ||
                    astcIntermediateTexture.height != astcIntermediateSizeHeight)
                {
                    astcIntermediateTexture.Release();
                    astcIntermediateTexture = null;
                }
            }
        }

#if UNITY_EDITOR
        // 在编辑器中仍然使用ASTC压缩。由于PC平台不支持ASTC，所以需要在压缩的同时会将压缩结果解压到uav中
        // 这个选项的目的是方便在编辑器中预览压缩后的效果
        private static bool s_DecompressInEditor = true;
#endif
        public static bool DecompressAstc()
        {
#if UNITY_EDITOR
            return s_DecompressInEditor;
#else
            return false;
#endif
        }

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

    public class ComputerShaderCompressor
    {
        private ComputeShader astcCompressorComputeShader; //compute shader 压缩
        private static int k_ResultId = Shader.PropertyToID("_Result");
        private static int k_DestRectId = Shader.PropertyToID("_DestRect");
        private static readonly int k_IntegerFromQuintsId = Shader.PropertyToID("integer_from_quints");
        private static readonly int k_ColorQuantTableId = Shader.PropertyToID("color_quant_table");
        private static readonly int k_DecompressTextureId = Shader.PropertyToID("_ResultDecompressed");
        private static int k_SourceTextureId = Shader.PropertyToID("_CompressSourceTexture");
        private static int k_SourceTextureMipLevelId = Shader.PropertyToID("_CompressSourceTexture_MipLevel");

        private int kernelHandle;

        public ComputerShaderCompressor(ComputeShader computeShader)
        {
            if (computeShader == null)
            {
                Debug.LogError("computeShader is null");
                return;
            }

            astcCompressorComputeShader = computeShader;
            kernelHandle = astcCompressorComputeShader.FindKernel("CSCompress");
        }


        public void Compressor(ASTC_BLOCKSIZE blockType, RenderTexture m_DecompressTexture, Texture sourceTexture,
            RenderTexture astcIntermediate)
        {
            if (astcCompressorComputeShader == null)
            {
                Debug.LogError("astcCompressorComputeShader is null");
                return;
            }

            ComputerShaderProperty(astcCompressorComputeShader, blockType);

            if (GPUTextureCompressorTools.DecompressAstc())
                astcCompressorComputeShader.SetTexture(kernelHandle, k_DecompressTextureId, m_DecompressTexture);

            astcCompressorComputeShader.SetTexture(kernelHandle, k_SourceTextureId, sourceTexture);
            astcCompressorComputeShader.SetVector(k_DestRectId,
                new Vector4(sourceTexture.width, sourceTexture.height, 1.0f / sourceTexture.width,
                    1.0f / sourceTexture.height));
            astcCompressorComputeShader.SetInt(k_SourceTextureMipLevelId, 0);
            astcCompressorComputeShader.SetTexture(kernelHandle, k_ResultId, astcIntermediate);
            astcCompressorComputeShader.SetVector("TextureSize",
                new Vector4(sourceTexture.width, sourceTexture.height, 0, 0));
            astcCompressorComputeShader.Dispatch(kernelHandle, astcIntermediate.width, astcIntermediate.height, 1);
        }


        private void ComputerShaderProperty(ComputeShader compressShader, ASTC_BLOCKSIZE blocksize)
        {
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

            if (GPUTextureCompressorTools.DecompressAstc())
                compressShader.EnableKeyword("_DECOMPRESS_RGB");
            else
                compressShader.DisableKeyword("_DECOMPRESS_RGB");
        }
    }

    public class FragmentShaderCompressor
    {
        private Material m_CompressMaterial;
        private Mesh m_FullScreenMesh = null;
        
        private ComputeShader astcCompressorComputeShader; //compute shader 压缩
        private static int k_ResultId = Shader.PropertyToID("_Result");
        private static int k_DestRectId = Shader.PropertyToID("_DestRect");
        private static readonly int k_IntegerFromQuintsId = Shader.PropertyToID("integer_from_quints");
        private static readonly int k_ColorQuantTableId = Shader.PropertyToID("color_quant_table");
        private static readonly int k_DecompressTextureId = Shader.PropertyToID("_ResultDecompressed");
        private static int k_SourceTextureId = Shader.PropertyToID("_CompressSourceTexture");
        private static int k_SourceTextureMipLevelId = Shader.PropertyToID("_CompressSourceTexture_MipLevel");

        public FragmentShaderCompressor(Shader compressShader)
        {
            m_CompressMaterial = new Material(compressShader);
            m_CompressMaterial.hideFlags = HideFlags.HideAndDontSave;
            
            if (!m_FullScreenMesh)
            {
                m_FullScreenMesh = new Mesh();
                m_FullScreenMesh.hideFlags = HideFlags.HideAndDontSave;
                m_FullScreenMesh.vertices = new []
                {
                    new Vector3(-1, -1, 0),
                    new Vector3(-1, 3, 0),
                    new Vector3(3, -1, 0),
                };
                m_FullScreenMesh.triangles = new [] { 0, 1, 2 }; 
                m_FullScreenMesh.RecalculateBounds();
            }
        }

        public void Compressor(ASTC_BLOCKSIZE blockType, RenderTexture m_DecompressTexture, Texture sourceTexture,
            RenderTexture astcIntermediate, bool srgb)
        {
            MaterialProperty(blockType);
            
            CommandBuffer cmd = CommandBufferPool.Get("GPU Texture Compress");
            cmd.SetRenderTarget(astcIntermediate);
            int rtWidth = astcIntermediate.width, rtHeight = astcIntermediate.height;
            cmd.SetViewport(new Rect(0, 0, rtWidth, rtHeight));
            if (GPUTextureCompressorTools.DecompressAstc())
                cmd.SetRandomWriteTarget(1, m_DecompressTexture);
            
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && srgb)
                cmd.EnableShaderKeyword("_GPU_COMPRESS_SRGB");
            else
                cmd.DisableShaderKeyword("_GPU_COMPRESS_SRGB");
            
            cmd.SetGlobalVector(k_DestRectId, new Vector4(sourceTexture.width, sourceTexture.height, 1.0f / sourceTexture.width, 1.0f / sourceTexture.height));
            cmd.SetGlobalInt(k_SourceTextureMipLevelId, 0);
            cmd.SetGlobalTexture(k_SourceTextureId, sourceTexture);
            
            cmd.DrawMesh(m_FullScreenMesh, Matrix4x4.identity, m_CompressMaterial, 0, 0);
            cmd.SetRenderTarget(BuiltinRenderTextureType.None);
            
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void MaterialProperty(ASTC_BLOCKSIZE blocksize)
        {
            if (blocksize == ASTC_BLOCKSIZE.ASTC_5x5)
            {
                m_CompressMaterial.EnableKeyword("_COMPRESS_ASTC5x5");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC4x4");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC6x6");
            }
            else if (blocksize == ASTC_BLOCKSIZE.ASTC_4x4)
            {
                m_CompressMaterial.EnableKeyword("_COMPRESS_ASTC4x4");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC5x5");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC6x6");
            }
            else
            {
                m_CompressMaterial.EnableKeyword("_COMPRESS_ASTC6x6");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC4x4");
                m_CompressMaterial.DisableKeyword("_COMPRESS_ASTC5x5");

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
                m_CompressMaterial.SetFloatArray(k_IntegerFromQuintsId, quintsLookup);
                m_CompressMaterial.SetFloatArray(k_ColorQuantTableId, colorQuantTable);
            }

            if (GPUTextureCompressorTools.DecompressAstc())
                m_CompressMaterial.EnableKeyword("_DECOMPRESS_RGB");
            else
                m_CompressMaterial.DisableKeyword("_DECOMPRESS_RGB");
        }
    }
}