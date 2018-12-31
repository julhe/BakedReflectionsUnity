//
// BakedReflections implementation for Unity by Julian Heinken (@schneckerstein) v1
//
// USAGE:
// 1. Place this script on the object you like to have reflections for.
//    NOTE: The implementation relies on the second uv channel (UV2). Therefore, it will only work if you activated "Generate Lightmap UVs" in the import settings of your mesh.
// 2. Change the shader of the material to "Unlit/displayBakedReflections" (or modify your own shader)
// 3. Modify "Slice Count Level" and "Resolution Level" to your preferences, click on "Start" to start baking the reflection atlas.
//    Its not recommended to let "Total Axis Size" exceed 8192, since this is the highest texture resolution unity is able to import later.
// 4. Click on "Export to Exr" to export the reflection atlas. (Default location is "Assets/Baked SurfaceReflections")

//MIT-License
//Copyright(c) 2018 Julian Heinken
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, 
//including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
//ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 

#if UNITY_EDITOR //TODO: find smater way
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEditor;
using UnityEngine;
using System.Linq; //TODO: remove for release
using UnityEngine.Rendering;
namespace SpecularLightmapping
{
    [ExecuteInEditMode]
    public class SpecularLightmapping : MonoBehaviour
    {
        private const string EXPORT_DIRECTORY = "Assets/Baked SurfaceReflections";

        public Material TargetMaterial;
        [Header("Options"), Space]
        public bool Dilation;

        [Range(1f, 10f)]
        public int sliceCountLevel = 2;
        [Range(1f, 10f)]
        public int resolutionLevel = 2;
        [Space]
        public int sliceCount = 16384;
        public int cubeMapResolution = 16;
        public int totalAtlasAxisSize;
        [Space]
        public bool start;
        [Space]
        public RenderTexture reflectionAtlas;

        const Texture2D.EXRFlags ExportExrFlags = Texture2D.EXRFlags.CompressZIP;
        public bool exportToExr;

        private GameObject renderCamera_GameObject;
        private Camera RenderCamera;

        [HideInInspector]
        public Shader ExtractShader, CubeMapTo2DTextureShader;


        struct Line
        {
            public Vector3 start, end;

            public Line(Vector3 start, Vector3 end)
            {
                this.start = start;
                this.end = end;
            }
        }

        private int slicesPerAxis = 0;
        void OnValidate()
        {
            sliceCount = Mathf.RoundToInt(Mathf.Pow(2f, sliceCountLevel * 2f)); // can only use even powers of two. from odd ones, there's no square root that is a power of two it self.
            cubeMapResolution = Mathf.RoundToInt(Mathf.Pow(2f, resolutionLevel));
            totalAtlasAxisSize = Mathf.RoundToInt(Mathf.Sqrt(sliceCount) * cubeMapResolution);
            slicesPerAxis = (Mathf.RoundToInt(Mathf.Sqrt(sliceCount)));
            Debug.Assert(Mathf.IsPowerOfTwo(slicesPerAxis));
        }
        void Awake()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (!meshRenderer)
                return;

            TargetMaterial = meshRenderer.sharedMaterial;
        }

        void Update () {
            if (start)
            {
                start = false;
                debugPoints.Clear();
                PrepareReflectionBuild();
                CurrentState = WorkerState.Baking;
                EditorApplication.update += EditorUpdate;
            }
            if (exportToExr)
            {
                exportToExr = false;
                ExportToFile(true);
            }
        }

        void GetReflectionProbePositions(out Vector3[] positions, out Vector3[] normals, out Vector4[] tangents, Shader ExtractShader, Matrix4x4 localToWorld, Mesh mesh, int sliceCount)
        {
            // setup extraction shader
            Material extractMaterial = new Material(ExtractShader);
            extractMaterial.SetPass(0);

            // create and setup renderbuffers
            RenderTexture worldPositionBuffer = new RenderTexture(slicesPerAxis, slicesPerAxis, 0, RenderTextureFormat.ARGBFloat);
            worldPositionBuffer.Create();
            RenderTexture worldNormalBuffer = new RenderTexture(slicesPerAxis, slicesPerAxis, 0, RenderTextureFormat.ARGBFloat);
            worldNormalBuffer.Create();
            RenderTexture worldTangentBuffer = new RenderTexture(slicesPerAxis, slicesPerAxis, 0, RenderTextureFormat.ARGBFloat);
            worldTangentBuffer.Create();

            RenderBuffer[] renderBuffers = new RenderBuffer[3];
            renderBuffers[0] = worldPositionBuffer.colorBuffer;
            renderBuffers[1] = worldNormalBuffer.colorBuffer;
            renderBuffers[2] = worldTangentBuffer.colorBuffer;

            RenderTexture depth = new RenderTexture(slicesPerAxis, slicesPerAxis, 32, RenderTextureFormat.Depth);
            RenderTargetSetup renderTargetSetup = new RenderTargetSetup(renderBuffers, depth.depthBuffer);
            RenderTexture prevRenderTarget = RenderTexture.active;
            Graphics.SetRenderTarget(renderTargetSetup);
    
            GL.Clear(true, true, Color.clear);

            // render the mesh
            Graphics.DrawMeshNow(mesh, localToWorld);

            // copy the result back from the GPU
            Texture2D samplePositionsCPU = new Texture2D(slicesPerAxis, slicesPerAxis, TextureFormat.RGBAFloat, false, true);
            RenderTexture.active = worldPositionBuffer;
            samplePositionsCPU.ReadPixels(new Rect(0, 0, samplePositionsCPU.width, samplePositionsCPU.height), 0, 0);
            samplePositionsCPU.Apply();

            Texture2D sampleNormalsCPU = new Texture2D(slicesPerAxis, slicesPerAxis, TextureFormat.RGBAFloat, false, true);
            RenderTexture.active = worldNormalBuffer;
            sampleNormalsCPU.ReadPixels(new Rect(0, 0, sampleNormalsCPU.width, sampleNormalsCPU.height), 0, 0);
            sampleNormalsCPU.Apply();

            Texture2D sampleTangentsCPU = new Texture2D(slicesPerAxis, slicesPerAxis, TextureFormat.RGBAFloat, false, true);
            RenderTexture.active = worldTangentBuffer;
            sampleTangentsCPU.ReadPixels(new Rect(0, 0, sampleTangentsCPU.width, sampleTangentsCPU.height), 0, 0);
            sampleTangentsCPU.Apply();

            RenderTexture.active = prevRenderTarget;
            positions = new Vector3[sliceCount];
            Color[] samplesPositionsColors = samplePositionsCPU.GetPixels();

            normals = new Vector3[sliceCount];
            Color[] samplesNormalColors = sampleNormalsCPU.GetPixels();

            tangents = new Vector4[sliceCount];
            Color[] samplesTangentColors = sampleTangentsCPU.GetPixels();

            // copy the results into usable arrays
            for (int i = 0; i < sliceCount; i++)
            {
                Color c = samplesPositionsColors[i];
                positions[i] = new Vector3(c.r, c.g, c.b);

                Color n = samplesNormalColors[i];
                normals[i] = new Vector3(n.r, n.g, n.b);

                Color t = samplesTangentColors[i];
                tangents[i] = new Vector4(t.r, t.g, t.b, t.a);
            }

            // cleanup
            worldPositionBuffer.Release();
            worldNormalBuffer.Release();
            worldTangentBuffer.Release();

            depth.Release();
            DestroyImmediate(samplePositionsCPU);
            DestroyImmediate(sampleNormalsCPU);
            DestroyImmediate(sampleTangentsCPU);
        }

        private const RenderTextureFormat DefaultRenderTextureFormat = RenderTextureFormat.ARGBHalf;
        private RenderTexture tmpCubemap;
        private Vector3[] samplesPositions, sampleNormals;
        private Vector4[] samplesTangent;
        private Material cubemapToTexture;
        private const string cubemapToTexture_HDR_keyword = "RENDERTARGET_HDR";
        void PrepareReflectionBuild()
        {
            isCovered = new bool[sliceCount];
            debugVectors.Clear();
            debugLines.Clear();
            ReflectionAtlas_AxisSize = Mathf.RoundToInt(Mathf.Sqrt(sliceCount) );

        GetReflectionProbePositions(
                out samplesPositions,
                out sampleNormals,
                out samplesTangent,
                ExtractShader, 
                transform.localToWorldMatrix,
                GetComponent<MeshFilter>().sharedMesh, 
                sliceCount);
    
            const int SuperSamplingAA = 8;  
            tmpCubemap = new RenderTexture(cubeMapResolution * SuperSamplingAA, cubeMapResolution * SuperSamplingAA, 32, DefaultRenderTextureFormat)
            {
                useMipMap = true,
                autoGenerateMips = true,
                dimension = TextureDimension.Cube
            };
            tmpCubemap.Create();

        

            if (reflectionAtlas != null)
            {
                reflectionAtlas.Release();
            }
            reflectionAtlas = new RenderTexture(ReflectionAtlas_AxisSize * cubeMapResolution, ReflectionAtlas_AxisSize * cubeMapResolution, 0, DefaultRenderTextureFormat)
            {
                anisoLevel = 0,
                useMipMap = false,
                autoGenerateMips = false
            };
            reflectionAtlas.Create();
            cubemapToTexture = new Material(CubeMapTo2DTextureShader); //TODO: release???

            workIndex = 0;
            needWork = true;

            if (!renderCamera_GameObject)
            {
                renderCamera_GameObject = new GameObject();
                renderCamera_GameObject.hideFlags = HideFlags.HideAndDontSave;
                RenderCamera = renderCamera_GameObject.AddComponent<Camera>();
                RenderCamera.clearFlags = CameraClearFlags.Skybox;
                RenderCamera.allowHDR = true;
            }

            if (TargetMaterial != null)
            {
                TargetMaterial.SetTexture("_ReflectionArray", reflectionAtlas);
                TargetMaterial.SetVector("_BakedReflectionParams", new Vector4(ReflectionAtlas_AxisSize, 1f / ReflectionAtlas_AxisSize));
            }
        }

        private bool needWork = false;
        private int workIndex, ReflectionAtlas_AxisSize;
        private WorkerState CurrentState = WorkerState.Idle;
        void EditorUpdate()
        {
            switch (CurrentState)
            {
                case WorkerState.Idle:
                    break;
                case WorkerState.Baking:
                    needWork = Work();
                    bool cancelByUser = EditorUtility.DisplayCancelableProgressBar(
                        "Baking Cubemap for " + this.name, 
                        workIndex + " / " + sliceCount,
                        workIndex / (float) sliceCount);
                    if (!needWork || cancelByUser)
                        CurrentState = WorkerState.Finish;
                    break;
                case WorkerState.Finish:                
                    bool anySliceCovered = isCovered.Cast<bool>().Any(x => x);

                    if (Dilation && anySliceCovered) //also take care over empty atlases to prevent infinity loops
                    {
                        Vector2 axisSizeRCP = new Vector2(1f / slicesPerAxis, 1f / slicesPerAxis);
                        Texture2D debugSlice = new Texture2D(cubeMapResolution, cubeMapResolution, TextureFormat.RGBAHalf, false, false);
                        for (int i = 0; i < sliceCount; i++)
                        {              
                            continue;
                            if(isCovered[i])
                                continue;
                            Vector2Int slice = IndexToCoordinate(i);
                            Vector2 uvCoord = axisSizeRCP * slice;
                            Color uvColor = new Color(uvCoord.x, uvCoord.y, 0, 0);
                            debugSlice.SetPixels(Enumerable.Repeat<Color>(uvColor, cubeMapResolution * cubeMapResolution).ToArray());
                            debugSlice.Apply();

                            Graphics.CopyTexture(
                                debugSlice, 0, 0,
                                0, //src X
                                0, //src Y
                                cubeMapResolution, //src width
                                cubeMapResolution, //src height

                                reflectionAtlas, 0, 0,
                                slice.x * cubeMapResolution, //dst X
                                slice.y * cubeMapResolution //dst Y
                            );
                        }

                        bool didAnyDialation = true; 
                        while(didAnyDialation) {
                            didAnyDialation = false;
                            for (int i = 0; i < sliceCount; i++) {
                                if(isCovered[i])
                                    continue;

                                if(Dilate(i)) {
                                    isCovered[i] = true;
                                    didAnyDialation = true;
                                }
                            }  
                        }

                        print("debugPoints=" + debugPoints.Count);
                    }

                // debugPoints = samplesPositions;
                    RenderTexture.active = null;
                    CleanUp();
                    CurrentState = WorkerState.Idle;
                    EditorApplication.update -= EditorUpdate;

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // src: https://stackoverflow.com/questions/620605/how-to-make-a-valid-windows-filename-from-an-arbitrary-string#620619
        static string ValidateFileName(string fileName)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }
        void ExportToFile(bool toEXR)
        {
            if (reflectionAtlas != null)
            {
                RenderTexture.active = reflectionAtlas;

                Texture2D reflectionAtlas_tex = new Texture2D(
                    reflectionAtlas.width, 
                    reflectionAtlas.height,
                    TextureFormat.RGBAHalf,
                    false, 
                    toEXR);
                reflectionAtlas_tex.ReadPixels(new Rect(0, 0, reflectionAtlas_tex.width, reflectionAtlas_tex.height), 0, 0);
                reflectionAtlas_tex.Apply();

                if (!System.IO.Directory.Exists(EXPORT_DIRECTORY))
                    System.IO.Directory.CreateDirectory(EXPORT_DIRECTORY);

                string path = EXPORT_DIRECTORY + "/" + ValidateFileName(this.name) + (toEXR ? ".exr" : ".png");
                System.IO.File.WriteAllBytes(path, toEXR ? reflectionAtlas_tex.EncodeToEXR(ExportExrFlags) : reflectionAtlas_tex.EncodeToPNG());
                print("exported reflection atlas to: " + path);

                UnityEditor.AssetDatabase.Refresh();
                RenderTexture.active = null;
                
                DestroyImmediate(reflectionAtlas_tex);
            }
        }
        void CleanUp()
        {
            EditorUtility.ClearProgressBar();
            if (tmpCubemap)
                tmpCubemap.Release();

            DestroyImmediate(renderCamera_GameObject);
        }
        private enum WorkerState
        {
            Idle,
            Baking,
            Finish,
        }

        bool[] isCovered;

        private Vector2Int IndexToCoordinate(int index) {
            return new Vector2Int(index % ReflectionAtlas_AxisSize, index / ReflectionAtlas_AxisSize);
        }

        private int CoordinateToIndex(Vector2Int coordinate) {   
            return coordinate.y * ReflectionAtlas_AxisSize + coordinate.x;
        }
        List<Line> debugLines = new List<Line>(), debugVectors = new List<Line>();
        private bool Work()
        {
            var start = DateTime.Now;
            const int MAX_TIME_PER_ITERATION = 30;
            while ((DateTime.Now - start).Milliseconds < MAX_TIME_PER_ITERATION && workIndex < sliceCount)
            {       
                Vector3 normal = sampleNormals[workIndex];

                if (normal != Vector3.zero)
                {
                    RenderCamera.transform.position = samplesPositions[workIndex];
                    RenderCamera.RenderToCubemap(tmpCubemap);

                    Vector3 tangent = samplesTangent[workIndex];
                    Vector3 biTangent = Vector3.Cross(normal, tangent) * samplesTangent[workIndex].w;

                    //construct world to hemisphere matrix
                    Matrix4x4 toHemisphere = new Matrix4x4(tangent, biTangent, normal, Vector4.zero);
                    cubemapToTexture.SetMatrix("normalToHemisphere", toHemisphere);
                    Vector2Int destination = IndexToCoordinate(workIndex);
        
                    float destinationX_01 = (destination.x * cubeMapResolution) / (float)reflectionAtlas.width;
                    float destinationY_01 = (destination.y * cubeMapResolution) / (float)reflectionAtlas.width;
                    float sliceSize_01 = cubeMapResolution / (float) reflectionAtlas.width;
                    cubemapToTexture.SetVector("_vertexTransform", new Vector4(sliceSize_01, sliceSize_01, destinationX_01, destinationY_01));
                    Graphics.Blit(tmpCubemap, reflectionAtlas, cubemapToTexture); 

                    isCovered[workIndex] = true;
                }
                workIndex++;
            }

        return workIndex < sliceCount;
        }

        /// <summary>
        /// Fills the slice with a convered neighbour slice
        /// </summary>
        /// <param name="index"></param>
        /// <returns>True if the dialation was succsessfull</returns>
        private bool Dilate(int index)
        {
            Texture2D debugSlice = new Texture2D(cubeMapResolution, cubeMapResolution, TextureFormat.RGBAHalf, false, false);
            debugSlice.SetPixels(Enumerable.Repeat<Color>(Color.red, cubeMapResolution * cubeMapResolution).ToArray());
            debugSlice.Apply();
            Vector2Int slice = IndexToCoordinate(index);
            for (int x = -1; x < 2; x++) {
                for (int y = -1; y < 2; y++) {
                    if(x == 0 && y == 0)
                        continue;

                    Vector2Int offset = new Vector2Int(x, y);
                    Vector2Int referenceSlicePosition = slice + offset;
                    // referenceSlicePosition.y = (slicesPerAxis - 1) - referenceSlicePosition.y;
                    bool isCoordinateInBounds = 
                        0 <= referenceSlicePosition.x && referenceSlicePosition.x < slicesPerAxis &&
                        0 <= referenceSlicePosition.y && referenceSlicePosition.y < slicesPerAxis;
                    
                    int referenceSliceIndex = CoordinateToIndex(referenceSlicePosition);
                    if (isCoordinateInBounds && isCovered[referenceSliceIndex]) 
                    {

                        debugPoints.Add(samplesPositions[referenceSliceIndex]);

                        Vector2 axisSizeRCP = new Vector2(1f / slicesPerAxis, 1f / slicesPerAxis);      
                        Vector2 uvCoord = axisSizeRCP * referenceSlicePosition;
                        Color uvColor = new Color(uvCoord.x, uvCoord.y, 0);
                        debugSlice.SetPixels(Enumerable.Repeat<Color>(uvColor, cubeMapResolution * cubeMapResolution).ToArray());
                        debugSlice.Apply();
                        RenderTexture tmpTexture = RenderTexture.GetTemporary(cubeMapResolution, cubeMapResolution, 0, DefaultRenderTextureFormat);
                        // can't have source- and destination texture to be the same object, so we first copy the slice into a temporal texture
                        Graphics.CopyTexture(
                            reflectionAtlas, 
                            0, //src element
                            0, //src mip
                            referenceSlicePosition.x * cubeMapResolution, //src X
                            referenceSlicePosition.y * cubeMapResolution, //src Y
                            cubeMapResolution, //src width
                            cubeMapResolution, //src height
                            tmpTexture, 
                            0, // dst element
                            0, // dst mip
                            0, // dst x
                            0  // dst y
                            );

                    //Graphics.Blit(debugSlice, tmpTexture);

                        Graphics.CopyTexture(
                            tmpTexture, 0, 0,
                            0, //src X
                            0, //src Y
                            cubeMapResolution, //src width
                            cubeMapResolution, //src height

                            reflectionAtlas, 0, 0,
                            slice.x * cubeMapResolution, //dst X
                            slice.y * cubeMapResolution //dst Y
                        );

                        RenderTexture.ReleaseTemporary(tmpTexture);
                        DestroyImmediate(debugSlice);
                        return true;
                    } else {
                        offset = offset;
                    }
                }
            }
            DestroyImmediate(debugSlice);
            return false;
        }

        private List<Vector3> debugPoints = new List<Vector3>();
        [Header("Debug")]
        public bool showDebugProbes;
        public float debugProbeScale = 1f;
        
        private void OnDrawGizmosSelected()
        {
            if (!showDebugProbes || debugPoints == null)
                return;

            Gizmos.color = Color.black;

            for (int i = 0; i < debugPoints.Count; i++)
            {
                if (i == debugPoints.Count - 1)
                {
                    Gizmos.color = Color.white;
                }
                Gizmos.DrawSphere(debugPoints[i], debugProbeScale);
                Gizmos.color = Color.gray;
            }
            for (int i = 0; i < debugLines.Count; i++)
            {
                Gizmos.DrawLine(debugLines[i].start, debugLines[i].end);
            }
            Gizmos.color = Color.red;
            for (int i = 0; i < debugVectors.Count; i++)
            {
                Gizmos.DrawLine(debugVectors[i].start, debugVectors[i].end);
            }
        }
        private void OnDestroy()
        {
            if (EditorApplication.update != null)
            {
                EditorApplication.update -= EditorUpdate;
            }
            CleanUp();
        }
    }
    #endif
}