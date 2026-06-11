using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Element color identity, shared by FX tinting and HUD theming:
    /// Void purple, Solar orange, Arc blue.
    /// </summary>
    public static class ElementPalette
    {
        public static Color Base(Element e) =>
            e == Element.Solar ? new Color(1f, 0.55f, 0.15f) :
            e == Element.Arc ? new Color(0.3f, 0.75f, 1f) :
            new Color(0.55f, 0.3f, 1f); // Void

        /// <summary>HDR variant for emissive materials and particle glow.</summary>
        public static Color Emission(Element e, float intensity = 2.5f) => Base(e) * intensity;
    }
}
