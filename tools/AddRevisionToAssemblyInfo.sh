#!/bin/bash
# This script appends a piece of metadata to the property file
# containing the Git revision, which will be available in the
# final assembly.

REV=$(git rev-parse HEAD)
if grep -q AssemblyInformationalVersion Properties/AssemblyInfo.cs
then
    "$(dirname "$0")/RemoveRevisionFromAssemblyInfo.sh"
fi
printf '[assembly: AssemblyInformationalVersion("%s")]\n' "$REV" >> Properties/AssemblyInfo.cs
