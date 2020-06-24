# Wivuu.GlobalCache

[Description]

## TODO
- Create github actions to publish releases
- Create nuget packages / publishing process
- Support IAsyncEnumerable in storage providers and global cache to stream data from storage

## Installation

This library can be installed via NuGet:

```sh
dotnet add package Wivuu.GlobalCache # Install core lib; includes filesystem adapter & JSON serializer
dotnet add package Wivuu.GlobalCache.AzureStorage # Install Azure Storage adapter
dotnet add package Wivuu.GlobalCache.BinarySerializer # Install Binary Serialization adapter
```

## Usage with DependencyInjection

Using standard Microsoft DI, GlobalCache can be included in your `Startup.cs`

```C#
using Wivuu.GlobalCache;
using Wivuu.GlobalCache.AzureStorage;
using Wivuu.GlobalCache.BinarySerializer;

public void ConfigureServices(IServiceCollection collection)
{
    // ...

    // Adds with default settings
    collection.AddWivuuGlobalCache();

    // OR configure additional settings
    collection.AddWivuuGlobalCache(options =>
    {
        // Use local storage emulator 
        var blobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
        var container         = blobServiceClient.GetBlobContainerClient("globalcache");

        // Create a storage container if it doesnt exist
        container.CreateIfNotExists();

        // Use blob storage provider
        options.StorageProvider = new BlobStorageProvider(container);

        // Use binary serialization provider
        options.SerializationProvider = new BinarySerializationProvider();
    });
}
```

## Usage in a controller

Once the global cache has been configured, it can be included using standard DI

```C#
[HttpGet]
public async Task<ActionResult<string>> GetExpensiveItemAsync([FromServices]IGlobalCache cache, 
                                                              [FromQuery]string name) =>
    // If the result is already cached, it will retrieve the cached value, otherwise
    // the (potentially expensive) provided generator function will be invoked and then the
    // result will be stored for future use
    await cache.GetOrCreateAsync(new CacheId("expensiveitems", name), async () =>
    {
        // Simulate hard work!
        await Task.Delay(5_000);

        // Or, you know, retrieve 1,000,000 items from a database and execute
        // expensive aggregation formulas no the resulting data and return it.

        return $"Hello {name}!";
    });

[HttpPut]
public async Task PutInvalidationAsync([FromServices]IGlobalCache cache) =>
    // Invalidate all names stored in the cache
    await cache.InvalidateAsync(CacheId.ForCategory("expensiveitems"));
```