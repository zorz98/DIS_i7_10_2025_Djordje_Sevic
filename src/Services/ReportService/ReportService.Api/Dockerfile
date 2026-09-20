FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Services/ReportService/ReportService.Api/ReportService.Api.csproj", "src/Services/ReportService/ReportService.Api/"]
COPY ["src/Services/ReportService/ReportService.Domain/ReportService.Domain.csproj", "src/Services/ReportService/ReportService.Domain/"]
COPY ["src/Services/ReportService/ReportService.Infrastructure/ReportService.Infrastructure.csproj", "src/Services/ReportService/ReportService.Infrastructure/"]
COPY ["src/BuildingBlocks/GreenFinance.Contracts/GreenFinance.Contracts.csproj", "src/BuildingBlocks/GreenFinance.Contracts/"]
RUN dotnet restore "src/Services/ReportService/ReportService.Api/ReportService.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/ReportService/ReportService.Api/ReportService.Api.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReportService.Api.dll"]
