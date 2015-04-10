#!/bin/bash
# This script removes the InformationalVersion metadata added by the
# AddRevision script so that the file appears unmodified.

grep -v 'AssemblyInformationalVersion' Properties/AssemblyInfo.cs > Properties/AssemblyInfo.tmp
cat Properties/AssemblyInfo.tmp > Properties/AssemblyInfo.cs
rm -f Properties/AssemblyInfo.tmp
