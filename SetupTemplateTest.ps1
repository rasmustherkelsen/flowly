dotnet pack -o ~/flowly-feed/ -c Release --version 10.0.0.0
dotnet nuget locals all --clear

dotnet new uninstall Flowly.Templates
dotnet pack src/Flowly.Templates/Flowly.Templates.csproj -c Release -o ./nupkg
dotnet new install ./nupkg/Flowly.Templates.1.0.0.nupkg