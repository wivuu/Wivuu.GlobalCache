param(
    [string]$key,
    [string]$version
)

function publish ($path) {
    $file = gci $path | sort LastWriteTime | select -last 1

    Write-Host "Publish $file ..."

    dotnet nuget push $file --source "github"
}

dotnet pack --configuration Release /p:version=$version

publish .\Wivuu.GlobalCache\bin\Release\*.nupkg
publish .\Wivuu.GlobalCache.AzureStorage\bin\Release\*.nupkg
publish .\Wivuu.GlobalCache.BinarySerializer\bin\Release\*.nupkg
