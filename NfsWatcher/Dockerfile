<<<<<<< HEAD
=======
# Etapa de build
>>>>>>> ea6ac57 (Project refactoring: new structure + interfaces + abstractions)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish

<<<<<<< HEAD
=======
# Etapa de rulare
>>>>>>> ea6ac57 (Project refactoring: new structure + interfaces + abstractions)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FileWatcherSMB.dll"]
