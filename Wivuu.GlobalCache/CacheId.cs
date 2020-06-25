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
        
        /// <summary>
        /// Create a new cache id representing an object
        /// </summary>
        /// <param name="category">Category of the object</param>
        /// <param name="hashcode">Hash of object to lookup</param>
        public CacheId(string category, object key)
        {
            Category = category;

            // Calculate hashcode from input object
            Hashcode = key switch
            {
                null             => 0,
                string keyString => GetStringHashCode(keyString),
                _                => key.GetHashCode()
            };
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

        /// <summary>
        /// Get stable hash for input string
        /// </summary>
        public static int GetStringHashCode(string? str)
        {
            if (str is null)
                return 0;
                
            unchecked
            {
                var hash1 = 5381;
                var hash2 = hash1;

                for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;

                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}