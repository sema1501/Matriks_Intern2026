FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5002

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/CryptoTracker.API/CryptoTracker.API.csproj", "CryptoTracker.API/"]
RUN dotnet restore "CryptoTracker.API/CryptoTracker.API.csproj"
COPY src/CryptoTracker.API/ CryptoTracker.API/
RUN dotnet build "CryptoTracker.API/CryptoTracker.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CryptoTracker.API/CryptoTracker.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CryptoTracker.API.dll"]
