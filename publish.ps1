param(
    [string]$key
)

function publish ($path) {
    $file = gci $path | sort LastWriteTime | select -last 1

    Write-Host "Publish $file ..."

    if ($key) {
        dotnet nuget push $file --source "nuget" -k $key --skip-duplicate
    }
    else {
        dotnet nuget push $file --source "nuget" --skip-duplicate
    }
}

dotnet pack --configuration Release

publish .\Wivuu.GlobalCache\bin\Release\*.nupkg
publish .\Wivuu.GlobalCache.AzureStorage\bin\Release\*.nupkg
publish .\Wivuu.GlobalCache.BinarySerializer\bin\Release\*.nupkg
