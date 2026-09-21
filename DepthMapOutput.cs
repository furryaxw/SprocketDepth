namespace SprocketDepth
{
    // Selects what a depth-map recording adds to the current command buffer.
    public enum DepthMapOutput
    {
        // Generate the normalized depth texture without drawing it.
        TextureOnly = 0,

        // Generate the texture and present it as grayscale to the currently
        // bound color target.
        TextureAndGrayscaleTarget = 1
    }
}
