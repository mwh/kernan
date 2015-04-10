# This script removes the InformationalVersion metadata added by the
# AddRevision script so that the file appears unmodified.

$out = Get-Content Properties\AssemblyInfo.cs |
    Select-String -NotMatch "AssemblyInformationalVersion"
$out | Set-Content Properties\AssemblyInfo.cs
