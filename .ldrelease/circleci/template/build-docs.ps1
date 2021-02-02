
# Standard build-docs.ps1 for .NET projects built on Windows, producing HTML documentation with
# Sandcastle Help File Builder. It builds a single documentation set that may include multiple
# projects.
#
# It has the following preconditions:
# - $LD_RELEASE_DOCS_TITLE is set to the displayable title of the documentation (not including
#   the version number); if this is not set, the script will not run
# - $LD_RELEASE_DOCS_TARGET_FRAMEWORK is the target framework of the build that the documentation
#   should be based on. The default is "net45". This must be a target framework that is built
#   by default when build.ps1 builds the solution.
# - $LD_RELEASE_DOCS_ASSEMBLIES is a space-delimited list of the names of all assemblies that are
#   to be documented (see below).
#
# The default is to document all of the assemblies whose projects are under .\src (like build.ps1
# it assumes that the subdirectories in .\src are named the same as their assemblies, and so
# are their project files). If you specify $LD_RELEASE_DOCS_ASSEMBLIES, you should provide the
# of the projects in .\src that you wish to document, and you can also add names of assemblies
# that are in your project's dependencies, if they were published with XML docs.
#

Write-Host
if ("$env:LD_RELEASE_DOCS_TITLE" -eq "") {
    Write-Host "Not generating documentation because LD_RELEASE_DOCS_TITLE was not set"
    exit
}
$targetFramework = "$env:LD_RELEASE_DOCS_TARGET_FRAMEWORK"
if ($targetFramework -eq "") {
    $targetFramework = "net45"
}
$assembliesParam = "$env:LD_RELEASE_DOCS_ASSEMBLIES"
if ($assembliesParam -eq "") {
    $sourceAssemblyNames = @()
} else {
    $sourceAssemblyNames = $assembliesParam -Split " "
}

# Terminate the script if any PowerShell command fails, or if we use an unknown variable
$ErrorActionPreference = "Stop"
Set-strictmode -version latest

# Disable PowerShell progress bars, which cause problems in CircleCI ("Access is denied ... while
# reading the console output buffer")
$ProgressPreference = "SilentlyContinue"

# Import helper functions and set up paths
$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\helpers.psm1" -Force
Import-Module "$scriptDir\sandcastle-helpers.psm1" -Force
$repoDir = Get-Location
$tempDir = "$HOME\temp"
CreateDirectoryIfNotExists -path $tempDir  # from helpers
$outputDir = "$tempDir\docs"

# Determine which assemblies we should document
$projects = GetSourceProjects -targetFramework $targetFramework
$sourceFilePaths = @()
if ($sourceAssemblyNames.count -eq 0) {
    $sourceAssemblyNames = $projects | %{$_.name}
}
foreach ($assemblyName in $sourceAssemblyNames) {
    $paths = GetSandcastleSourceFilePaths `
        -assemblyName $assemblyName `
        -projects $projects  # from sandcastle-helpers
    $sourceFilePaths = $sourceFilePaths + $paths
}

# Create the Sandcastle project file
$projectXmlPath = "$tempDir\project.shfbproj"
$projectXml = BuildSandcastleProjectXml `
    -templatePath "$scriptDir\template.shfbproj" `
    -sourceFilePaths $sourceFilePaths `
    -projects $projects `
    -outputDir $outputDir  # from sandcastle-helpers
Set-Content -Path $projectXmlPath -Value $projectXml

InstallSandcastle -tempDir $tempDir  # from sandcastle-helpers

# Run the help builder via msbuild
DeleteAndRecreateDirectory -path $outputDir  # from helpers
Write-Host
Write-Host "Building documentation"
Write-Host
ExecuteOrFail { msbuild $projectXmlPath }

# Add our own stylesheet overrides. You're supposed to be able to put customized stylesheets in
# ./styles (relative to the project file) and have them be automatically copied in, but that
# doesn't seem to work, so we'll just modify the CSS file after building.
Get-Content "$scriptDir\docs-override.css" | Add-Content "$outputDir\styles\branding-Website.css"

# Make an archive of the output and store it as a single artifact
Zip -sourcePath $outputDir -zipFile $repoDir/artifacts/docs.zip  # from helpers
