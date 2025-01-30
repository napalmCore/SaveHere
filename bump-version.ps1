
# Usage Examples:
# .\bump-version.ps1 -VersionType major
# .\bump-version.ps1 -VersionType minor
# .\bump-version.ps1 -VersionType patch
# .\bump-version.ps1 (same as patch)

# If running by double-click, set default to patch
if ([Environment]::GetCommandLineArgs().Length -eq 1) {
    $VersionType = "patch"
} else {
    param (
        [Parameter(Mandatory=$false)]
        [ValidateSet("major", "minor", "patch")]
        [string]$VersionType = "patch"
    )
}

$csprojPath = "SaveHere/SaveHere/SaveHere.csproj"
$content = Get-Content $csprojPath

# Find the current version
$versionPattern = '<Version>(\d+)\.(\d+)\.(\d+)</Version>'
$match = [regex]::Match($content, $versionPattern)
if ($match.Success) {
    $major = [int]$match.Groups[1].Value
    $minor = [int]$match.Groups[2].Value
    $patch = [int]$match.Groups[3].Value

    # Increment based on type
    switch ($VersionType) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
    }

    $newVersion = "$major.$minor.$patch"
    $newContent = $content -replace $versionPattern, "<Version>$newVersion</Version>"
    Set-Content $csprojPath $newContent

    Write-Host "Version bumped to $newVersion"
}

# Keep window open when double-clicked
if ([Environment]::GetCommandLineArgs().Length -eq 1) {
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}