namespace SprocketDepth
{
    /// <summary>
    /// Describes the resources used by the most recently recorded depth map.
    /// </summary>
    public readonly struct HdrpDepthMapFrameInfo
    {
        internal HdrpDepthMapFrameInfo(
            int width,
            int height,
            float maxDistanceMeters,
            string cameraName,
            string sourceDepthTextureName,
            string normalizedDepthTextureName,
            string computeShaderName,
            int computeKernel,
            string presentationShaderName,
            int presentationPass,
            DepthMapOutput output)
        {
            Width = width;
            Height = height;
            MaxDistanceMeters = maxDistanceMeters;
            CameraName = cameraName;
            SourceDepthTextureName = sourceDepthTextureName;
            NormalizedDepthTextureName = normalizedDepthTextureName;
            ComputeShaderName = computeShaderName;
            ComputeKernel = computeKernel;
            PresentationShaderName = presentationShaderName;
            PresentationPass = presentationPass;
            Output = output;
        }

        /// <summary>Recorded viewport width in pixels.</summary>
        public int Width { get; }

        /// <summary>Recorded viewport height in pixels.</summary>
        public int Height { get; }

        /// <summary>Distance represented by black in the normalized map.</summary>
        public float MaxDistanceMeters { get; }

        /// <summary>Hierarchy path of the camera that produced the map.</summary>
        public string CameraName { get; }

        /// <summary>Name of HDRP's source depth-pyramid texture.</summary>
        public string SourceDepthTextureName { get; }

        /// <summary>Name of the generated normalized RFloat texture.</summary>
        public string NormalizedDepthTextureName { get; }

        /// <summary>Name of the compute shader used for linearization.</summary>
        public string ComputeShaderName { get; }

        /// <summary>Compute kernel index used for linearization.</summary>
        public int ComputeKernel { get; }

        /// <summary>Name of the shader used for optional presentation.</summary>
        public string PresentationShaderName { get; }

        /// <summary>Pass index used for optional presentation.</summary>
        public int PresentationPass { get; }

        /// <summary>Output mode recorded for the frame.</summary>
        public DepthMapOutput Output { get; }

        /// <summary>Builds a compact diagnostic description.</summary>
        public override string ToString()
        {
            return $"camera={CameraName}, size={Width}x{Height}, " +
                   $"range=0-{MaxDistanceMeters:F0}m, " +
                   $"source={SourceDepthTextureName}, " +
                   $"normalized={NormalizedDepthTextureName}, " +
                   $"compute={ComputeShaderName}/kernel-{ComputeKernel}, " +
                   $"present={PresentationShaderName}/pass-{PresentationPass}, " +
                   $"output={Output}";
        }
    }
}
