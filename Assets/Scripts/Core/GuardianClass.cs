namespace Density3.Core
{
    /// <summary>The three playable classes, each bound to one element.</summary>
    public enum GuardianClass
    {
        Warlock,
        Hunter,
        Titan
    }

    public static class GuardianClassExtensions
    {
        /// <summary>Warlock = Void, Hunter = Solar, Titan = Arc.</summary>
        public static Element ElementOf(this GuardianClass g) =>
            g == GuardianClass.Hunter ? Element.Solar :
            g == GuardianClass.Titan ? Element.Arc :
            Element.Void;
    }
}
