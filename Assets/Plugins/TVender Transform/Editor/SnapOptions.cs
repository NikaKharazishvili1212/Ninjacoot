
namespace TVender.VTransform
{

    internal enum SnapOptions
    {
        /// <summary>
        /// use the outermost point of the game object.
        /// </summary>
        Auto = 1,
        /// <summary>
        /// use the center point of the outermost triangular surface.
        /// </summary>
        TriangleCenter = 2,
        /// <summary>
        /// use game object center
        /// </summary>
        Center = 4,
        /// <summary>
        /// use outermost vertex
        /// </summary>
        Vertex = 8,
        /// <summary>
        /// use the outermost of the collider
        /// </summary>
        Collider = 16
    }
}