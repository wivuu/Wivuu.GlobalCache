#!/usr/bin/env dotnet fsi --langversion:preview

#r "nuget: Farmer, 0.23.0"
#r "nuget: Argu, 6.1.1"

open System
open Argu
open Farmer
open Farmer.Builders

type Args =
    | Storage_Name of path:string
    | Location of loc:string
    | Resource_Group of rg:string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Storage_Name _ -> "Specify the name of your storage account (default: random)"
            | Location _ -> "Specify the location of your resource group (default: eastus)"
            | Resource_Group _ -> "Specify the name of your resource group name (default: globalcache)"

let parser = ArgumentParser.Create<Args>(programName = "deploy.azurestorage.fsx")
let args   = parser.ParseCommandLine(fsi.CommandLineArgs |> Array.skip 1, raiseOnUsage=false)

if args.IsUsageRequested then
    parser.PrintUsage() |> printf "%s"
    exit 0

let storageName = args.GetResult(Storage_Name, "cache" + Guid.NewGuid().ToString("n").Substring(0, 8))
let loc         = args.GetResult(Location, "eastus") |> Location.Location
let rg          = args.GetResult(Resource_Group, "globalcache")

// Create a storage account
let storageAcct = storageAccount {
    name storageName
    sku Storage.Premium_LRS
    // TODO:
    // tier Premium
    // kind "BlockBlobStorage"
    //   "properties": {
    //     "supportsHttpsTrafficOnly": true,
    //     "accessTier": "Hot"
    //   },
    // managementPolicies
}

// Create an ARM template
let deployment = arm {
    location loc
    add_resources [ storageAcct ]
    output "storage_key" storageAcct.Key
}

// Deploy to Azure
let outputs = deployment |> Deploy.execute rg Deploy.NoParameters

printfn """
// Add this to your Startup.cs
services.AddWivuuGlobalCache(options =>
{
    // TODO: Store this securely in your configuration or key vault
    var connString = "%s";
    var container  = new Azure.Storage.Blobs.BlobContainerClient(connString, "globalcache");
    
    // Create a new container to store your cached items
    container.CreateIfNotExists();

    options.StorageProvider = new Wivuu.GlobalCache.AzureStorage.BlobStorageProvider(container);
});

""" (outputs.["storage_key"])

printfn "Your storage key: %s" (outputs.["storage_key"])