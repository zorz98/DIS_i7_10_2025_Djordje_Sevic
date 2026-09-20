FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/ReferenceDataService/ReferenceDataService.Api/ReferenceDataService.Api.csproj", "src/Services/ReferenceDataService/ReferenceDataService.Api/"]
COPY ["src/Services/ReferenceDataService/ReferenceDataService.Domain/ReferenceDataService.Domain.csproj", "src/Services/ReferenceDataService/ReferenceDataService.Domain/"]
COPY ["src/Services/ReferenceDataService/ReferenceDataService.Infrastructure/ReferenceDataService.Infrastructure.csproj", "src/Services/ReferenceDataService/ReferenceDataService.Infrastructure/"]
COPY ["src/BuildingBlocks/GreenFinance.ServiceDiscovery/GreenFinance.ServiceDiscovery.csproj", "src/BuildingBlocks/GreenFinance.ServiceDiscovery/"]
RUN dotnet restore "src/Services/ReferenceDataService/ReferenceDataService.Api/ReferenceDataService.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/ReferenceDataService/ReferenceDataService.Api/ReferenceDataService.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReferenceDataService.Api.dll"]
