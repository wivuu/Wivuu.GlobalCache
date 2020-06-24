namespace Wivuu.GlobalCache
{
    public class GlobalCacheSettings
    {
        /// <summary>
        /// Serialization provider to use - if none specified, will default to JSON serialization
        /// </summary>
        public ISerializationProvider? SerializationProvider { get; set; } 

        /// <summary>
        /// Storage provider to use - if none specified, will default to filesystem storage
        /// 
        /// Other options:
        /// - Azure Blob Storage (install Wivuu.GlobalCache.AzureStorage)
        /// </summary>
        public IStorageProvider? StorageProvider { get; set; } 
    }
}