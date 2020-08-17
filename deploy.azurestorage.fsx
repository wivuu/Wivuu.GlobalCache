#!/usr/bin/env dotnet fsi --langversion:preview

#r "nuget: Farmer, 0.23.0"
#r "nuget: Argu, 6.1.1"

open System
open Argu
open Farmer
open Farmer.Builders

type Args =
    | Storage_Name   of path:string
    | Location       of location:string
    | Resource_Group of name:string
    | Out_File       of path:string
    | Premium

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Storage_Name _   -> "Specify the name of your storage account (default: random)"
            | Location _       -> "Specify the location of your resource group (default: eastus)"
            | Resource_Group _ -> "Specify the name of your resource group name (default: globalcache)"
            | Out_File _       -> "Output to a file rather than deploying (default: None)"
            | Premium          -> "(FUTURE) Create a premium blockblob storage account with lower latency (default: off)"

let parser = ArgumentParser.Create<Args>(programName = "deploy.azurestorage.fsx")
let args   = parser.ParseCommandLine(fsi.CommandLineArgs.[1..], raiseOnUsage=false)

if args.IsUsageRequested then
    parser.PrintUsage() |> printf "%s"
    exit 0

let storageName = args.GetResult(Storage_Name, "cache" + Guid.NewGuid().ToString("n").Substring(0, 8))
let loc         = args.GetResult(Location, "eastus") |> Location.Location
let rg          = args.GetResult(Resource_Group, "globalcache")
let outFile     = args.GetResult(Out_File, "")
let premium     = args.Contains (Premium)

// Create a storage account
let storageAcct = storageAccount {
    name storageName
    sku (
        if premium then Storage.Premium_LRS 
        else Storage.Standard_LRS
    )
    // TODO:
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
    add_resource storageAcct
    output "storage_key" storageAcct.Key
}

let format: Printf.TextWriterFormat<_> = """
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
"""

match outFile with
| "" ->
    // Deploy to Azure
    let outputs = deployment |> Deploy.execute rg Deploy.NoParameters
    printfn format (outputs.["storage_key"])
    printfn "Your storage key: %s" (outputs.["storage_key"])
| path ->
    // Save to file
    let path = path |> System.IO.Path.GetFileNameWithoutExtension
    deployment |> Writer.quickWrite path
    printfn format "DefaultEndpointsProtocol=https;your-storage-key-here!"