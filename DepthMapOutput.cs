namespace SprocketDepth
{
    /// <summary>
    /// Selects what a depth-map recording adds to the current command buffer.
    /// </summary>
    public enum DepthMapOutput
    {
        /// <summary>
        /// Generate the normalized depth texture without drawing it.
        /// </summary>
        TextureOnly = 0,

        /// <summary>
        /// Generate the texture and present it as grayscale to the currently
        /// bound color target.
        /// </summary>
        TextureAndGrayscaleTarget = 1
    }
}
