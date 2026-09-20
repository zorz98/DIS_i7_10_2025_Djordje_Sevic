FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/TransactionService/TransactionService.Api/TransactionService.Api.csproj", "src/Services/TransactionService/TransactionService.Api/"]
COPY ["src/Services/TransactionService/TransactionService.Domain/TransactionService.Domain.csproj", "src/Services/TransactionService/TransactionService.Domain/"]
COPY ["src/Services/TransactionService/TransactionService.Infrastructure/TransactionService.Infrastructure.csproj", "src/Services/TransactionService/TransactionService.Infrastructure/"]
COPY ["src/BuildingBlocks/GreenFinance.Contracts/GreenFinance.Contracts.csproj", "src/BuildingBlocks/GreenFinance.Contracts/"]
COPY ["src/BuildingBlocks/GreenFinance.ServiceDiscovery/GreenFinance.ServiceDiscovery.csproj", "src/BuildingBlocks/GreenFinance.ServiceDiscovery/"]
RUN dotnet restore "src/Services/TransactionService/TransactionService.Api/TransactionService.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/TransactionService/TransactionService.Api/TransactionService.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TransactionService.Api.dll"]
