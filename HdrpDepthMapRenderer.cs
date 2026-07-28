using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SprocketDepth
{
    /// <summary>
    /// Records native HDRP depth linearization and optional grayscale
    /// presentation commands without rendering the scene a second time.
    /// </summary>
    public sealed class HdrpDepthMapRenderer : IDisposable
    {
        /// <summary>Default white-near depth range.</summary>
        public const float DefaultMaxDistanceMeters = 300.0f;

        private const string DepthTextureGlobalName = "_CameraDepthTexture";
        private const string LinearizeComputeName = "DepthOfFieldCoC";
        private const string LinearizeKernelName = "KMainManual";
        private const string PresentationShaderName = "Hidden/HDRP/Blit";
        private const int PresentationPass = 20;

        private static readonly int DepthTextureGlobalId =
            Shader.PropertyToID(DepthTextureGlobalName);
        private static readonly int ComputeInputId =
            Shader.PropertyToID("_DepthMinMaxAvg");
        private static readonly int ComputeOutputId =
            Shader.PropertyToID("_OutputCoCTexture");
        private static readonly int ComputeParamsId =
            Shader.PropertyToID("_Params");
        private static readonly int BlitTextureId =
            Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId =
            Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitScaleBiasRtId =
            Shader.PropertyToID("_BlitScaleBiasRt");
        private static readonly int BlitMipLevelId =
            Shader.PropertyToID("_BlitMipLevel");
        private static readonly int BlitTextureSizeId =
            Shader.PropertyToID("_BlitTextureSize");
        private static readonly int BlitArraySliceId =
            Shader.PropertyToID("_BlitTexArraySlice");

        private ComputeShader? linearizeCompute;
        private int linearizeKernel = -1;
        private RenderTexture? normalizedDepthTexture;
        private int textureWidth;
        private int textureHeight;
        private Material? presentationMaterial;
        private MaterialPropertyBlock? presentationProperties;
        private int presentationPass = -1;
        private int diagnosticCameraId = int.MinValue;
        private string diagnosticCameraName = "<none>";
        private int diagnosticSourceDepthId = int.MinValue;
        private string diagnosticSourceDepthName = "<unnamed>";
        private string normalizedDepthTextureName = "<unavailable>";
        private bool disposed;

        /// <summary>
        /// Creates a white-near renderer with a fixed normalized depth range.
        /// </summary>
        /// <param name="maxDistanceMeters">
        /// Positive finite distance that maps to black.
        /// </param>
        public HdrpDepthMapRenderer(
            float maxDistanceMeters = DefaultMaxDistanceMeters)
            : this(maxDistanceMeters, whiteNear: true)
        {
        }

        /// <summary>
        /// Creates a renderer with a fixed normalized depth range and polarity.
        /// </summary>
        /// <param name="maxDistanceMeters">
        /// Positive finite distance that maps to the far endpoint.
        /// </param>
        /// <param name="whiteNear">
        /// True for 1-distance/range; false for distance/range.
        /// </param>
        public HdrpDepthMapRenderer(
            float maxDistanceMeters,
            bool whiteNear)
        {
            if (!float.IsFinite(maxDistanceMeters) || maxDistanceMeters <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDistanceMeters),
                    maxDistanceMeters,
                    "Depth-map range must be finite and greater than zero.");
            }

            MaxDistanceMeters = maxDistanceMeters;
            WhiteNear = whiteNear;
        }

        /// <summary>
        /// Distance mapped to the far endpoint in the normalized texture.
        /// </summary>
        public float MaxDistanceMeters { get; }

        /// <summary>
        /// True when near maps to white; false when far maps to white.
        /// </summary>
        public bool WhiteNear { get; }

        /// <summary>
        /// The reusable RFloat texture populated by the most recent successful
        /// call. Values are either 1-distance/range or distance/range.
        /// </summary>
        public RenderTexture? NormalizedDepthTexture => normalizedDepthTexture;

        /// <summary>
        /// Metadata for the most recently recorded frame.
        /// </summary>
        public HdrpDepthMapFrameInfo? LastFrameInfo { get; private set; }

        /// <summary>
        /// Failure text from the most recent unsuccessful call, otherwise an
        /// empty string.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Records depth-map commands into the Custom Pass command buffer.
        /// Call this while HDRP is executing a camera-scoped Custom Pass after
        /// the camera depth pyramid has been produced.
        /// </summary>
        public bool TryRecord(
            CustomPassContext context,
            DepthMapOutput output = DepthMapOutput.TextureAndGrayscaleTarget)
        {
            if (disposed)
                return Fail("The depth-map renderer has been disposed.");
            if (output != DepthMapOutput.TextureOnly &&
                output != DepthMapOutput.TextureAndGrayscaleTarget)
            {
                return Fail($"Unsupported depth-map output: {output}.");
            }

            try
            {
                CommandBuffer? commandBuffer = context.cmd;
                if (commandBuffer == null)
                    return Fail("The Custom Pass command buffer is unavailable.");

                Texture? depthAtlas = Shader.GetGlobalTexture(
                    DepthTextureGlobalId);
                if (depthAtlas == null)
                {
                    return Fail(
                        "HDRP global depth pyramid _CameraDepthTexture is unavailable.");
                }

                if (!TryResolveViewport(
                        context,
                        output,
                        out int width,
                        out int height,
                        out string viewportError))
                {
                    return Fail(viewportError);
                }

                if (!EnsureLinearizeResources(width, height))
                    return false;
                if (linearizeCompute == null || normalizedDepthTexture == null ||
                    linearizeKernel < 0)
                {
                    return Fail("Linear depth resources are incomplete.");
                }

                commandBuffer.SetComputeTextureParam(
                    linearizeCompute,
                    linearizeKernel,
                    ComputeInputId,
                    new RenderTargetIdentifier(depthAtlas));
                commandBuffer.SetComputeTextureParam(
                    linearizeCompute,
                    linearizeKernel,
                    ComputeOutputId,
                    new RenderTargetIdentifier(normalizedDepthTexture));
                commandBuffer.SetComputeVectorParam(
                    linearizeCompute,
                    ComputeParamsId,
                    WhiteNear
                        ? new Vector4(
                            -1.0f,
                            0.0f,
                            MaxDistanceMeters,
                            0.0f)
                        : new Vector4(
                            -1.0f,
                            0.0f,
                            0.0f,
                            MaxDistanceMeters));
                commandBuffer.DispatchCompute(
                    linearizeCompute,
                    linearizeKernel,
                    (width + 7) / 8,
                    (height + 7) / 8,
                    1);

                if (output == DepthMapOutput.TextureAndGrayscaleTarget)
                {
                    if (!EnsurePresentationResources())
                        return false;
                    RecordGrayscalePresentation(commandBuffer);
                }

                Camera? camera = context.hdCamera == null
                    ? null
                    : context.hdCamera.camera;
                UpdateDiagnosticNames(camera, depthAtlas);
                LastFrameInfo = new HdrpDepthMapFrameInfo(
                    width,
                    height,
                    MaxDistanceMeters,
                    diagnosticCameraName,
                    diagnosticSourceDepthName,
                    normalizedDepthTextureName,
                    LinearizeComputeName,
                    linearizeKernel,
                    output == DepthMapOutput.TextureOnly
                        ? "<not-presented>"
                        : PresentationShaderName,
                    output == DepthMapOutput.TextureOnly ? -1 : presentationPass,
                    output);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception.ToString());
            }
        }

        /// <summary>
        /// Releases the owned material and render texture. Call on Unity's
        /// main thread after the last queued command that uses the texture.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ReleaseNormalizedDepthTexture();
            if (presentationMaterial != null)
                UnityEngine.Object.Destroy(presentationMaterial);
            presentationMaterial = null;
            presentationProperties = null;
            presentationPass = -1;
            linearizeCompute = null;
            linearizeKernel = -1;
            diagnosticCameraId = int.MinValue;
            diagnosticCameraName = "<none>";
            diagnosticSourceDepthId = int.MinValue;
            diagnosticSourceDepthName = "<unnamed>";
            LastFrameInfo = null;
            GC.SuppressFinalize(this);
        }

        private bool EnsureLinearizeResources(int width, int height)
        {
            if (linearizeCompute == null)
            {
                var candidates = Resources.FindObjectsOfTypeAll<ComputeShader>();
                var inventory = new StringBuilder();
                foreach (ComputeShader candidate in candidates)
                {
                    if (candidate == null)
                        continue;

                    string candidateName = candidate.name ?? string.Empty;
                    if (candidateName.IndexOf(
                            LinearizeComputeName,
                            StringComparison.OrdinalIgnoreCase) >= 0 &&
                        candidateName.IndexOf(
                            "Reproject",
                            StringComparison.OrdinalIgnoreCase) < 0 &&
                        candidateName.IndexOf(
                            "Dilate",
                            StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        linearizeCompute = candidate;
                        break;
                    }

                    if ((candidateName.IndexOf(
                             "Depth",
                             StringComparison.OrdinalIgnoreCase) >= 0 ||
                         candidateName.IndexOf(
                             "CoC",
                             StringComparison.OrdinalIgnoreCase) >= 0) &&
                        inventory.Length < 1024)
                    {
                        if (inventory.Length > 0)
                            inventory.Append(" | ");
                        inventory.Append(candidateName);
                    }
                }

                if (linearizeCompute == null)
                {
                    return Fail(
                        $"Compute shader {LinearizeComputeName} is unavailable. " +
                        $"Depth/CoC candidates: [{inventory}]");
                }

                linearizeKernel = linearizeCompute.FindKernel(
                    LinearizeKernelName);
                if (linearizeKernel < 0)
                {
                    return Fail(
                        $"Compute kernel {LinearizeKernelName} is unavailable in " +
                        $"{linearizeCompute.name}.");
                }
            }

            if (normalizedDepthTexture != null &&
                textureWidth == width &&
                textureHeight == height &&
                normalizedDepthTexture.IsCreated())
            {
                return true;
            }

            ReleaseNormalizedDepthTexture();
            var descriptor = new RenderTextureDescriptor(
                width,
                height,
                RenderTextureFormat.RFloat,
                0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 1,
                msaaSamples = 1,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false
            };
            normalizedDepthTextureName =
                $"SprocketDepth_NormalizedLinear_" +
                $"{(WhiteNear ? "WhiteNear" : "WhiteFar")}_" +
                $"{MaxDistanceMeters:F0}m";
            normalizedDepthTexture = new RenderTexture(descriptor)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = normalizedDepthTextureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!normalizedDepthTexture.Create())
            {
                ReleaseNormalizedDepthTexture();
                return Fail(
                    $"Failed to create {width}x{height} RFloat depth texture.");
            }

            textureWidth = width;
            textureHeight = height;
            return true;
        }

        private bool EnsurePresentationResources()
        {
            if (presentationMaterial != null &&
                presentationProperties != null &&
                presentationPass == PresentationPass)
            {
                return true;
            }

            Shader? shader = Shader.Find(PresentationShaderName);
            if (shader == null)
                return Fail($"Shader {PresentationShaderName} is unavailable.");

            presentationMaterial ??= new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "SprocketDepth_GrayscalePresentation"
            };
            presentationProperties ??= new MaterialPropertyBlock();
            if (presentationMaterial.passCount <= PresentationPass)
            {
                return Fail(
                    $"Shader {PresentationShaderName} requires pass " +
                    $"{PresentationPass}, but exposes " +
                    $"{presentationMaterial.passCount} passes.");
            }

            presentationPass = PresentationPass;
            return true;
        }

        private void RecordGrayscalePresentation(CommandBuffer commandBuffer)
        {
            if (presentationMaterial == null ||
                presentationProperties == null ||
                normalizedDepthTexture == null ||
                presentationPass < 0)
            {
                throw new InvalidOperationException(
                    "Grayscale presentation resources are incomplete.");
            }

            float width = Mathf.Max(1.0f, normalizedDepthTexture.width);
            float height = Mathf.Max(1.0f, normalizedDepthTexture.height);
            presentationProperties.Clear();
            presentationProperties.SetTexture(
                BlitTextureId,
                normalizedDepthTexture);
            presentationProperties.SetVector(
                BlitScaleBiasId,
                new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
            presentationProperties.SetVector(
                BlitScaleBiasRtId,
                new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
            presentationProperties.SetFloat(BlitMipLevelId, 0.0f);
            presentationProperties.SetVector(
                BlitTextureSizeId,
                new Vector4(width, height, 1.0f / width, 1.0f / height));
            presentationProperties.SetInt(BlitArraySliceId, 0);
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                presentationMaterial,
                presentationPass,
                MeshTopology.Quads,
                4,
                1,
                presentationProperties);
        }

        private static bool TryResolveViewport(
            CustomPassContext context,
            DepthMapOutput output,
            out int width,
            out int height,
            out string error)
        {
            RTHandle? destination = context.cameraColorBuffer;
            if (destination != null && destination.rt != null &&
                destination.rt.IsCreated())
            {
                RTHandleProperties properties = destination.rtHandleProperties;
                Vector2Int scaled = destination.GetScaledSize(
                    properties.currentViewportSize);
                width = scaled.x > 0 ? scaled.x : destination.rt.width;
                height = scaled.y > 0 ? scaled.y : destination.rt.height;
                if (width > 0 && height > 0)
                {
                    error = string.Empty;
                    return true;
                }
            }

            if (output == DepthMapOutput.TextureOnly &&
                context.hdCamera != null &&
                context.hdCamera.camera != null)
            {
                Camera camera = context.hdCamera.camera;
                width = camera.pixelWidth;
                height = camera.pixelHeight;
                if (width > 0 && height > 0)
                {
                    error = string.Empty;
                    return true;
                }
            }

            width = 0;
            height = 0;
            error = output == DepthMapOutput.TextureAndGrayscaleTarget
                ? "The current camera color target is unavailable for presentation."
                : "A valid camera viewport is unavailable.";
            return false;
        }

        private void ReleaseNormalizedDepthTexture()
        {
            if (normalizedDepthTexture != null)
            {
                if (normalizedDepthTexture.IsCreated())
                    normalizedDepthTexture.Release();
                UnityEngine.Object.Destroy(normalizedDepthTexture);
            }

            normalizedDepthTexture = null;
            normalizedDepthTextureName = "<unavailable>";
            textureWidth = 0;
            textureHeight = 0;
        }

        private bool Fail(string error)
        {
            LastError = error;
            LastFrameInfo = null;
            return false;
        }

        private void UpdateDiagnosticNames(Camera? camera, Texture depthAtlas)
        {
            int cameraId = camera == null ? 0 : camera.GetInstanceID();
            if (cameraId != diagnosticCameraId)
            {
                diagnosticCameraId = cameraId;
                diagnosticCameraName = camera == null
                    ? "<none>"
                    : BuildTransformPath(camera.transform);
            }

            int depthId = depthAtlas.GetInstanceID();
            if (depthId != diagnosticSourceDepthId)
            {
                diagnosticSourceDepthId = depthId;
                diagnosticSourceDepthName = depthAtlas.name ?? "<unnamed>";
            }
        }

        private static string BuildTransformPath(Transform? transform)
        {
            if (transform == null)
                return "<none>";

            string path = transform.name ?? "<unnamed>";
            Transform? current = transform.parent;
            while (current != null)
            {
                path = $"{current.name ?? "<unnamed>"}/{path}";
                current = current.parent;
            }
            return path;
        }
    }
}
