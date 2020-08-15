namespace Wivuu.GlobalCache
{
    /// <summary>
    /// Indicator which indicates object can have an ETag value
    /// </summary>
    public interface IHasEtag
    {
        string? ETag { get; set; }
    }
}