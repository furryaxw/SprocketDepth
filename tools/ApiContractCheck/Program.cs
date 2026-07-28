using SprocketDepth;

static HdrpDepthMapRenderer CreateLegacy(float range) =>
    new(range);

static HdrpDepthMapRenderer CreatePolarityAware(float range) =>
    new(range, whiteNear: false);

using HdrpDepthMapRenderer legacy = CreateLegacy(300.0f);
using HdrpDepthMapRenderer polarityAware =
    CreatePolarityAware(300.0f);

return legacy.WhiteNear || !polarityAware.WhiteNear ? 0 : 1;
