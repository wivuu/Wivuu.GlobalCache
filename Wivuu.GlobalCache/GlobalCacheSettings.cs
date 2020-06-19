namespace Wivuu.GlobalCache
{
    public class GlobalCacheSettings
    {
        ISerializationProvider DefaultSerializationProvider { get; set; } = new JsonSerializationProvider();
    }
}