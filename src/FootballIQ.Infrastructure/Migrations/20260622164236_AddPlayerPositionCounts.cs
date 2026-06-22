using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPositionCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_position_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_position_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_position_counts_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_position_counts_PlayerId_Position",
                table: "player_position_counts",
                columns: new[] { "PlayerId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_position_counts");
        }
    }
}
