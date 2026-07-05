using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShelfScout.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConsumptionStat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumption_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    household_id = table.Column<int>(type: "integer", nullable: false),
                    period_month = table.Column<DateOnly>(type: "date", nullable: false),
                    location_kind = table.Column<string>(type: "text", nullable: true),
                    removal_reason = table.Column<string>(type: "text", nullable: false),
                    category_label = table.Column<string>(type: "text", nullable: false),
                    category_is_global = table.Column<bool>(type: "boolean", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumption_stats", x => x.id);
                    table.ForeignKey(
                        name: "fk_consumption_stats_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumption_stats_household_id_period_month_location_kind_r",
                table: "consumption_stats",
                columns: new[] { "household_id", "period_month", "location_kind", "removal_reason", "category_label", "category_is_global" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumption_stats");
        }
    }
}
