# Wivuu.GlobalCache
[![Tests](https://github.com/wivuu/Wivuu.GlobalCache/workflows/Tests/badge.svg)](https://github.com/wivuu/Wivuu.GlobalCache/actions?query=workflow%3ATests)

[![wivuu.globalcache](https://img.shields.io/nuget/v/wivuu.globalcache.svg?label=wivuu.globalcache)](https://www.nuget.org/packages/Wivuu.GlobalCache/)
[![wivuu.globalcache.AzureStorage](https://img.shields.io/nuget/v/wivuu.globalcache.azurestorage.svg?label=wivuu.globalcache.azurestorage)](https://www.nuget.org/packages/Wivuu.GlobalCache.AzureStorage)
[![wivuu.globalcache.BinarySerializer](https://img.shields.io/nuget/v/wivuu.globalcache.binaryserializer.svg?label=wivuu.globalcache.binaryserializer)](https://www.nuget.org/packages/Wivuu.GlobalCache.BinarySerializer)


The GlobalCache library endeavors to provide a cheap and effortless way to host a distributed caching mechanism.

This is a great fit if:
- You want to keep costs down by avoiding use of VMs or services like Redis.
- The API surface area is sufficient for your needs (GetOrCreate and Invalidate).
- You write to/update a datasource infrequently, but querying and aggregating data out is common and CPU/memory/database intensive; simply call `InvalidateAsync` on the cache whenever a write happens and then `GetOrCreate` in the distributed consumer so that the aggregation logic only happens once per write.

![](images/2020-06-25-10-24-55.png)

*The above sample demonstrates pulling data from the database and processing it (above the red line) vs. downloading pre-cached 100kb from premium blob storage (below the red line)*

## Progress
- [View board](https://github.com/wivuu/Wivuu.GlobalCache/projects/1)
- [View issues](https://github.com/wivuu/Wivuu.GlobalCache/issues)

## Azure Blob Storage
Using azure blob storage provider you can utilize Premium Block Blob to get consistent low latency, datacenter local cache that can be shared across many instances of your application or even by Azure Functions. You can configure Lifecycle Management on your container to automatically expire categories of cached item and detect changes to your cache using the change feed or azure function blob triggers. 

## Installation

This library can be installed via NuGet:

```sh
# Install the core library; includes filesystem adapter & JSON serializer
dotnet add package Wivuu.GlobalCache 

# Or install the azure storage adapter directly
dotnet add package Wivuu.GlobalCache.AzureStorage # Install Azure Storage adapter
dotnet add package Wivuu.GlobalCache.BinarySerializer # Optionally: Install Binary Serialization adapter
dotnet add package Wivuu.GlobalCache.Web # Optionally: Install ASP.NET globalcache attribute
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

## Usage with DI

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

## Using the GlobalCache attribute with ASP.NET Core

The easiest way to use Wivuu GlobalCache with ASP.NET is with the `GlobalCache` attribute. 
The attribute contains several options for specifying which cached item to pull, such as varying by parameters, headers, or by your own custom logic (by implementing `IGlobalCacheExpiration`). You can also set a duration on the attribute which floors the current date by your specified duration, so if you want requests to get updated information every hour, you would use `3600` seconds, and a new cache key would be generated every hour your action is requested.

### How it works
The `GlobalCache` works by adding a `IAsyncActionFilter` which generates a predictable cache id inside your `category`, based on the request parameters, route, and attribute settings, then it attempts to open up a read stream to the cached location. If that cached response is not found, it execute your action as normal and append an HTTP Context item for later in the pipeline. Otherwise it will send the cached response back.

The `GlobalCache` attribute additionally implements the `IAsyncResultFilter` which checks if a cache id is present in the HTTP context; if it is it will intercept your actions response stream and multiplex it to the client AND to the cache backing storage.

### Install the package

```
dotnet add package Wivuu.GlobalCache.Web
```

Once the package is installed, and the service is configured in your Startup.cs, simply add the attribute to your actions, like below.

```C#
[HttpGet]
[GlobalCache("weather/byday", VaryByParam="days")]
public async Task<IList<WeatherItem>> GetAsync([FromQuery]int days = 100)
{
    var start  = DateTime.Now.AddDays(-days);
    var report = await MyWeatherAPI.GetWeatherSinceAsync(start);

    return report;
}
```
