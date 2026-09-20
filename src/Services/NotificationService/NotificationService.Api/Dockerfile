FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/NotificationService/NotificationService.Api/NotificationService.Api.csproj", "src/Services/NotificationService/NotificationService.Api/"]
COPY ["src/Services/NotificationService/NotificationService.Domain/NotificationService.Domain.csproj", "src/Services/NotificationService/NotificationService.Domain/"]
COPY ["src/Services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "src/Services/NotificationService/NotificationService.Infrastructure/"]
COPY ["src/BuildingBlocks/GreenFinance.Contracts/GreenFinance.Contracts.csproj", "src/BuildingBlocks/GreenFinance.Contracts/"]
COPY ["src/BuildingBlocks/GreenFinance.ServiceDiscovery/GreenFinance.ServiceDiscovery.csproj", "src/BuildingBlocks/GreenFinance.ServiceDiscovery/"]
RUN dotnet restore "src/Services/NotificationService/NotificationService.Api/NotificationService.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/NotificationService/NotificationService.Api/NotificationService.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
