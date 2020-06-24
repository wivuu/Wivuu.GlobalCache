namespace Wivuu.GlobalCache
{
    public struct CacheIdentity
    {
        public string Category { get; }

        public int? Hashcode { get; set; }

        public CacheIdentity(string category, int? hashcode = default)
        {
            Category = category;
            Hashcode = hashcode;
        }

        public bool IsCategory => !Hashcode.HasValue;

        public override string ToString() => 
            Hashcode.HasValue 
            ? $"{Category}/{Hashcode}"
            : $"{Category}";
    }
}