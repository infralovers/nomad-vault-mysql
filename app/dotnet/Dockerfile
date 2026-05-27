FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src/NomadVaultMySqlDotnet/

COPY NomadVaultMySqlDotnet.csproj ./
RUN dotnet restore NomadVaultMySqlDotnet.csproj

COPY . ./
RUN dotnet publish NomadVaultMySqlDotnet.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish ./

VOLUME ["/app/config"]

ENTRYPOINT ["dotnet", "NomadVaultMySqlDotnet.dll"]
