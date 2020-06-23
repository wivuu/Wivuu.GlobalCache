# Wivuu.GlobalCache

TODO: Support IAsyncEnumerable in storage provider

- Store serialized either as JSON or BinaryPack https://github.com/Sergio0694/BinaryPack#:~:text=To%20summarize%3A,to%20245x%20faster%20than%20Newtonsoft.
- Store filename as {category}/{per-category-hash}.bin
- Expire based on {category}
- Expire based on time w/ blob lifetime
- Example
    - Summary:
        - Category: DashboardSummary
        - Key: partner, startdate, enddate
        - Invalidations: "new match", "status change"
    - LocationInfo:
        - Category: MapLeads
        - Key: partner, startdate, enddate
- StorageProviders:
    - Filesystem
    - Azure Blob Storage
    - Memory
- SerializationProviders:
    - Json ( System.Text.Json )
    - Binary ( https://github.com/Sergio0694/BinaryPack )

- API:
    ```C#
    // Retrieve item from cache (or create it)
    GlobalCache.GetOrCreateAsync("Summary", hashcode_key, async (e) => {
        // Execute expensive uncached operation here
        #if DEBUG
        e.SerializeAsJson = true;
        #endif

        return expensiveResultToBeSerialized;
    });
    
    // Invalidate item in cache
    GlobalCache.InvalidateAsync("Summary"[, hashcode_key]);

    // Additional:
    GlobalCache.CreateAsync(...);
    GlobalCache.GetAsync(...);
    ```