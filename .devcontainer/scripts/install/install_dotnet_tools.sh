#!/bin/bash

# add development certificate, so the server runs
# note: it won't be trusted
#
# see here for more information
# https://stackoverflow.com/questions/55485511/how-to-run-dotnet-dev-certs-https-trust

echo $'\n\nInstalling HTTPS development certificate'
echo "<<##########################################>>"
dotnet dev-certs https
echo "<\##########################################/>"

echo $'\n\nInstalling dotnet-ef tool globally'
echo "<<##########################################>>"
dotnet tool install dotnet-ef -g --version 10.0.13
echo "<\##########################################/>"

echo $'\n\nInstalling libman tool globally'
echo "<<##########################################>>"
dotnet tool install -g Microsoft.Web.LibraryManager.Cli --version 3.0.114
echo "<\##########################################/>"

echo $'\n\nInstalling dotnet-aspnet-codegenerator tool globally'
echo "<<##########################################>>"
dotnet tool install -g dotnet-aspnet-codegenerator --version 10.0.2
echo "<\##########################################/>"

echo $'\n\nInstalling csharpier tool globally'
echo "<<##########################################>>"
dotnet tool install -g csharpier
echo "<\##########################################/>"
