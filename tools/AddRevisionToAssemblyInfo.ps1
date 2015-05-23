# This script appends a piece of metadata to the property file
# containing the Git revision, which will be available in the
# final assembly.

# First remove any existing property so we don't double up
$out = Get-Content Properties\AssemblyInfo.cs | Select-String -NotMatch "AssemblyInformationalVersion"
$out | Set-Content Properties\AssemblyInfo.cs

# Check for git.exe in the path
$GIT = Get-Command -ErrorAction SilentlyContinue "git.exe"

# Otherwise, check some well-known locations where git.exe
# might be found
if (! $GIT) {
    $GIT = @(
        # GitHub for Windows
        (Resolve-Path -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\GitHub\PortableGit_*"),
        # Installed by Visual Studio on demand
        "C:\Program Files (x86)\Git",
        "C:\Program Files\Git"
    ) |
        Where-Object { $_ } |
        Where-Object { Test-Path $_ } |
        Select -First 1

    if (! $GIT) {
        echo "Git not found, skipping revision embedding."
        exit
    }

    $GIT = "$GIT\cmd\git.exe"
}

$x = & "$GIT" describe --long --dirty="++" --always
Add-Content Properties\AssemblyInfo.cs "[assembly: AssemblyInformationalVersion(`"$x`")]"
