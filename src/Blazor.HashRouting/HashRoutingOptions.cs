namespace Blazor.HashRouting
{
    /// <summary>
    /// Defines settings for hash-based routing.
    /// </summary>
    public sealed class HashRoutingOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether URLs are canonicalized to hash routes.
        /// </summary>
        public bool CanonicalizeToHash { get; set; } = true;

        /// <summary>
        /// Gets or sets the hash prefix used for route values.
        /// </summary>
        public string HashPrefix { get; set; } = "/";

        /// <summary>
        /// Gets or sets a value indicating whether internal anchor links are intercepted.
        /// </summary>
        public bool InterceptInternalLinks { get; set; } = true;
    }
}
