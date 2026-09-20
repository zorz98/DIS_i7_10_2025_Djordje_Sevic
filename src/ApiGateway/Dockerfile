FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/ApiGateway/ApiGateway.csproj", "src/ApiGateway/"]
COPY ["src/BuildingBlocks/GreenFinance.ServiceDiscovery/GreenFinance.ServiceDiscovery.csproj", "src/BuildingBlocks/GreenFinance.ServiceDiscovery/"]
RUN dotnet restore "src/ApiGateway/ApiGateway.csproj"

COPY . .
RUN dotnet publish "src/ApiGateway/ApiGateway.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
