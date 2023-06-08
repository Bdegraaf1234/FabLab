# this script should be run from the root directory of the fablab solution


# first we clear the nuget caches so we don't have old dependencies in there. This might be gratuitous but doesn't hurt
dotnet nuget locals all --clear

# then we clean the old files, restore the nuget packages and build the projects on local config. This builds all projects. 
# After FabLab.csproj is built, all non-system dlls in the output folder are copied to .\NugetBuilder\folder for packing
# when dependencies.csproj is built directly afterwards, these dlls are packed into a nuget (name: dependencies, version taken from dependencies.csproj)
# this nuget ends up in the .\nuget
dotnet clean /p:configuration=local
dotnet restore /p:configuration=local
dotnet build /p:configuration=local

# we then clear the chache again, just to be sure
dotnet nuget locals all --clear

# then we build the projects on release config. This builds only fablab.csproj, with the .\nuget folder as an additional restore source (defined in fablab.csproj)
dotnet restore /p:configuration=release
dotnet build /p:configuration=release

# forn convenience, we restore local again
dotnet restore /p:configuration=local