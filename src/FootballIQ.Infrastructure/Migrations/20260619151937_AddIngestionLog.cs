using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_log",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StatsBombMatchId = table.Column<int>(type: "integer", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_log_StatsBombMatchId",
                table: "ingestion_log",
                column: "StatsBombMatchId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_log");
        }
    }
}
