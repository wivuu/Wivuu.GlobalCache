namespace Wivuu.GlobalCache
{
    public class GlobalCacheSettings
    {
        public ISerializationProvider? DefaultSerializationProvider { get; set; } 

        public IStorageProvider? DefaultStorageProvider { get; set; } 
    }
}