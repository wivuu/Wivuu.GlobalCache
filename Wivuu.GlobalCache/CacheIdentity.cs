namespace Wivuu.GlobalCache
{
    public struct CacheIdentity
    {
        public string Category { get; }

        public int Hashcode { get; set; }

        public CacheIdentity(string category, int hashcode = 0)
        {
            Category = category;
            Hashcode = hashcode;
        }
    }
}