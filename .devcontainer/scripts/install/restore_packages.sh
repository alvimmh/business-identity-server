#!/bin/bash

echo $'\n\nRestoring packages'
echo "<<##########################################>>"
dotnet restore --force src/BusinessIdentityServer/BusinessIdentityServer.csproj
echo "<\##########################################/>"

