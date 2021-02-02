
function InstallSandcastle {
    param(
        [Parameter(Mandatory)][string]$tempDir
    )
    $ErrorActionPreference = "Stop"
    $sandcastleInstallerUrl = "https://github.com/EWSoftware/SHFB/releases/download/v2019.8.24.0/SHFBInstaller_v2019.8.24.0.zip"

    $http = New-Object System.Net.WebClient
    Write-Host "Downloading Sandcastle installer"
    $http.DownloadFile($sandcastleInstallerUrl, "$tempDir\sandcastle-installer.zip")

    Write-Host "Unzipping"
    Unzip -zipFile "$tempDir\sandcastle-installer.zip" -destination $tempDir  # from helpers

    Write-Host "Running installer"
    $installerPath = "$tempDir\InstallResources\SandcastleHelpFileBuilder.msi"
    Start-Process msiexec.exe -Wait -ArgumentList "/I $installerPath /quiet"
    [System.Environment]::SetEnvironmentVariable("SHFBROOT", `
        "C:\Program Files (x86)\EWSoftware\Sandcastle Help File Builder\", "Process")
}

function GetSandcastleSourceFilePaths {
    param(
        [Parameter(Mandatory)][string]$assemblyName,
        [Parameter(Mandatory)][hashtable[]]$projects
    )
    $foundPaths = @()
    $nugetCacheDir = "$HOME\.nuget\packages"

    # The DLL for this assembly should always be in the build products. The XML file may or
    # may not be; if it isn't, try to get it from the NuGet cache.
    $foundDll = $false
    $foundXml = $false
    foreach ($project in $projects) {
        $basePath = "$($project.buildProductsDir)\$assemblyName"
        if (Test-Path "$basePath.dll") {
            $foundDll = $true
            $foundPaths += "$basePath.dll"
        }
        if (Test-Path "$basePath.xml") {
            $foundXml = $true
            $foundPaths += "$basePath.xml"
        }
    }

    if (-not $foundDll) {
        throw "Could not find $assemblyName.dll in build products"
    }
    if (-not $foundXml) {
        # Find the dependency version for this assembly in one of our project files
        foreach ($project in $projects) {
            $targetFramework = $project.buildProductsDir | Split-Path -leaf
            $match = Select-String -Path $project.projectFilePath `
                -Pattern "<PackageReference.*""$assemblyName"".*""([^""]*)"""
            if ($match.Matches.Length -eq 1) {
                $dependencyVersion = $match.Matches[0].Groups[1].Value
                $xmlFilePath = "$nugetCacheDir\$assemblyName\$dependencyVersion\lib\$targetFramework\$assemblyName.xml"
                if (Test-Path $xmlFilePath) {
                    $foundXml = $true
                    $foundPaths += $xmlFilePath
                } else {
                    throw "Could not find $assemblyName.xml in NuGet cache"
                }
                break
            }
        }
        if (-not $foundXml) {
            throw "Could not find $assemblyName.xml version in build products or NuGet cache"
        }
    }

    return $foundPaths
}

function BuildSandcastleProjectXml {
    param(
        [Parameter(Mandatory)][string]$templatePath,
        [Parameter(Mandatory)][string[]]$sourceFilePaths,
        [Parameter(Mandatory)][hashtable[]]$projects,
        [Parameter(Mandatory)][string]$outputDir
    )
    $template = Get-Content $templatePath -Raw
    $sourcesXml = ""
    foreach ($sourceFilePath in $sourceFilePaths) {
        $sourcesXml += "<DocumentationSource sourceFile=""$sourceFilePath""/>"
    }
    $dependenciesXml = ""
    # Assume that every DLL in the build products that isn't one of our source projects is a dependency
    $dependenciesAlreadyDone = @()
    foreach ($project in $projects) {
        $dlls = dir "$($project.buildProductsDir)\*.dll"
        foreach ($dll in $dll) {
            $assemblyName = $dll.Name.TrimEnd(".dll")
            if (-not $dependenciesAlreadyDone.Contains($assemblyName)) {
                $dependenciesXml += "<Reference Include=""$assemblyName"">" +
                    "<HintPath>$(dll.FullName)</HintPath>" +
                    "</Reference>";
                $dependenciesAlreadyDone += $assemblyName;
            }
        }
    }
    return $template -Replace "{{TITLE}}", "$env:LD_RELEASE_DOCS_TITLE $env:LD_RELEASE_VERSION" `
        -Replace "{{SOURCES}}", $sourcesXml `
        -Replace "{{OUTPUT_PATH}}", $outputDir `
        -Replace "{{DEPENDENCIES}}", $dependenciesXml
}
