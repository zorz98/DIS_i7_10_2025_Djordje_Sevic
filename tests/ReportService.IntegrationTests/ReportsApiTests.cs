using System.Net.Http.Json;
using FluentAssertions;
using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ReportService.IntegrationTests;

public class ReportsApiTests(ReportServiceApiFactory factory) : IClassFixture<ReportServiceApiFactory>
{
    private sealed record CompanyReportDto(
        int CompanyId, int Year, int Month, decimal TotalExpenses, decimal TotalCo2, double? EsgScore, int Transactions);

    [Fact]
    public async Task GetCompanyReport_Should_Aggregate_Events_Published_Onto_The_Bus()
    {
        var companyId = 4242;
        var transactionId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 20);

        using (var scope = factory.Services.CreateScope())
        {
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            await publishEndpoint.Publish(new TransactionCreatedEvent(
                transactionId, companyId, "Fuel", 5000m, "EUR", date));

            await publishEndpoint.Publish(new EsgCalculatedEvent(
                transactionId, companyId, "Fuel", 11550m, 54, 31, DateTimeOffset.UtcNow));
        }

        var client = factory.CreateClient();
        CompanyReportDto? report = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500);
            var response = await client.GetAsync($"/reports/company/{companyId}?month={date.Month}&year={date.Year}");
            response.EnsureSuccessStatusCode();
            report = await response.Content.ReadFromJsonAsync<CompanyReportDto>();
            if (report is { Transactions: > 0, EsgScore: not null })
            {
                break;
            }
        }

        report.Should().NotBeNull();
        report!.TotalExpenses.Should().Be(5000m);
        report.TotalCo2.Should().Be(11550m);
        report.EsgScore.Should().Be(31d);
        report.Transactions.Should().Be(1);
    }

    [Fact]
    public async Task GetCompanyReport_Should_Return_Zeroed_Report_For_Company_With_No_Data()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/reports/company/999999?month=1&year=2020");
        response.EnsureSuccessStatusCode();

        var report = await response.Content.ReadFromJsonAsync<CompanyReportDto>();

        report.Should().NotBeNull();
        report!.Transactions.Should().Be(0);
        report.TotalExpenses.Should().Be(0m);
    }
}
