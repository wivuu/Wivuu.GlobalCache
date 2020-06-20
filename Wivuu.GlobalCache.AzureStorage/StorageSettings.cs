namespace Wivuu.GlobalCache.AzureStorage
{
    public class StorageSettings
    {
        public string? ConnectionString { get; set; }

        public string ContainerName { get; set; } = "GlobalCache";
    }
}