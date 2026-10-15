#!/bin/bash

echo $'\n\nInitializing main_db'
echo "<<##########################################>>"
dotnet ef database update --context ApplicationDbContext --project src/BusinessIdentityServer/
echo "<\##########################################/>"
