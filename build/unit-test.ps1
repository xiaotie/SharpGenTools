Param(
    [bool] $RunOpenCover = $true,
    [string] $Configuration = "Debug"
)

function Test
{
    Param([string] $project, [bool] $collectCoverage)

    $dotnetExe = ./build/find-exe "dotnet"
    
    $arguments = @("test", "$project/$project.csproj", "--no-build", "--no-restore", "-c $Configuration")

    $filters = @("+[SharpGen]*", "+[SharpGen.Runtime]*")

    if($collectCoverage) {
        return ./build/Run-OpenCover -executable $dotnetExe -arguments $arguments -filters $filters 
    }
    else {
        dotnet test "$project/$project.csproj" --no-build --no-restore -c $Configuration | Write-Host
        return $LastExitCode -eq 0
    }

}

if(!(Test -project "SharpGen.UnitTests" -collectCoverage $RunOpenCover)) {
    Write-Error "SharpGen Unit Tests Failed"
    return $false
}

if(!(Test -project "SharpGen.Runtime.UnitTests" -collectCoverage $RunOpenCover)) {
    Write-Error "SharpGen.Runtime Unit Tests Failed"
    return $false
}

return $true