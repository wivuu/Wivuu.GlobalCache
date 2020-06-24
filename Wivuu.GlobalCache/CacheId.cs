namespace Wivuu.GlobalCache
{
    /// <summary>
    /// Global cache id
    /// </summary>
    public struct CacheId
    {
        /// <summary>
        /// The broad category or namespace of cache items
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// The hashcode of an individual item
        /// </summary>
        public int? Hashcode { get; private set; }

        /// <summary>
        /// Create a new cache id representing an object
        /// </summary>
        /// <param name="category">Category of the object</param>
        /// <param name="hashcode">Hash of object to lookup</param>
        public CacheId(string category, int hashcode)
        {
            Category = category;
            Hashcode = hashcode;
        }
        
        private CacheId(string category)
        {
            Category = category;
            Hashcode = null;
        }

        /// <summary>
        /// Create an id of a category / namespace
        /// </summary>
        /// <param name="category">Category or namespace of cached items</param>
        public static CacheId ForCategory(string category) => new CacheId(category);

        /// <summary>
        /// Does the CacheId refer to a category of items or a specific item
        /// </summary>
        public bool IsCategory => !Hashcode.HasValue;

        public override string ToString() => 
            Hashcode.HasValue 
            ? $"{Category}/{Hashcode}"
            : $"{Category}";
    }
}