FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/ESGService/ESGService.Api/ESGService.Api.csproj", "src/Services/ESGService/ESGService.Api/"]
COPY ["src/Services/ESGService/ESGService.Domain/ESGService.Domain.csproj", "src/Services/ESGService/ESGService.Domain/"]
COPY ["src/Services/ESGService/ESGService.Infrastructure/ESGService.Infrastructure.csproj", "src/Services/ESGService/ESGService.Infrastructure/"]
COPY ["src/BuildingBlocks/GreenFinance.Contracts/GreenFinance.Contracts.csproj", "src/BuildingBlocks/GreenFinance.Contracts/"]
RUN dotnet restore "src/Services/ESGService/ESGService.Api/ESGService.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/ESGService/ESGService.Api/ESGService.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ESGService.Api.dll"]
