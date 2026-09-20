using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ReferenceDataService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmissionFactors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Co2FactorPerEur = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmissionFactors", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EmissionFactors",
                columns: new[] { "Id", "Category", "Co2FactorPerEur" },
                values: new object[,]
                {
                    { 1, "Fuel", 2.31m },
                    { 2, "Electricity", 0.45m },
                    { 3, "Flights", 2.50m },
                    { 4, "PublicTransport", 0.10m },
                    { 5, "OfficeSupplies", 0.20m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmissionFactors_Category",
                table: "EmissionFactors",
                column: "Category",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmissionFactors");
        }
    }
}
