namespace Wivuu.GlobalCache
{
    public struct CacheIdentity
    {
        public string Category { get; }

        public int? Hashcode { get; set; }

        public CacheIdentity(string category, int hashcode)
        {
            Category = category;
            Hashcode = hashcode;
        }
        
        private CacheIdentity(string category)
        {
            Category = category;
            Hashcode = null;
        }

        public static CacheIdentity ForCategory(string category) => new CacheIdentity(category);

        public bool IsCategory => !Hashcode.HasValue;

        public override string ToString() => 
            Hashcode.HasValue 
            ? $"{Category}/{Hashcode}"
            : $"{Category}";
    }
}