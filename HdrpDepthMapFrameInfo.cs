namespace SprocketDepth
{
    // Describes the resources used by the most recently recorded depth map.
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

        // Recorded viewport width in pixels.
        public int Width { get; }

        // Recorded viewport height in pixels.
        public int Height { get; }

        // Distance represented by black in the normalized map.
        public float MaxDistanceMeters { get; }

        // Hierarchy path of the camera that produced the map.
        public string CameraName { get; }

        // Name of HDRP's source depth-pyramid texture.
        public string SourceDepthTextureName { get; }

        // Name of the generated normalized RFloat texture.
        public string NormalizedDepthTextureName { get; }

        // Name of the compute shader used for linearization.
        public string ComputeShaderName { get; }

        // Compute kernel index used for linearization.
        public int ComputeKernel { get; }

        // Name of the shader used for optional presentation.
        public string PresentationShaderName { get; }

        // Pass index used for optional presentation.
        public int PresentationPass { get; }

        // Output mode recorded for the frame.
        public DepthMapOutput Output { get; }

        // Builds a compact diagnostic description.
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
