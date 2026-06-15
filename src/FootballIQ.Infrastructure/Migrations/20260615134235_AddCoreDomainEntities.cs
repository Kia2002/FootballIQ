using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StatsBombTeamId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StatsBombPlayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreferredFoot = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StatsBombMatchId = table.Column<int>(type: "integer", nullable: false),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HomeClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeScore = table.Column<int>(type: "integer", nullable: false),
                    AwayScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_matches_clubs_AwayClubId",
                        column: x => x.AwayClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_matches_clubs_HomeClubId",
                        column: x => x.HomeClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "player_season_stats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    SeasonId = table.Column<int>(type: "integer", nullable: false),
                    MatchesPlayed = table.Column<int>(type: "integer", nullable: false),
                    MinutesPlayed = table.Column<int>(type: "integer", nullable: false),
                    PassesCompleted = table.Column<int>(type: "integer", nullable: false),
                    PassesAttempted = table.Column<int>(type: "integer", nullable: false),
                    Goals = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    ExpectedGoals = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedAssists = table.Column<double>(type: "double precision", nullable: false),
                    Pressures = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_season_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_season_stats_clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_season_stats_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clubs_StatsBombTeamId",
                table: "clubs",
                column: "StatsBombTeamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matches_AwayClubId",
                table: "matches",
                column: "AwayClubId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_HomeClubId",
                table: "matches",
                column: "HomeClubId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_StatsBombMatchId",
                table: "matches",
                column: "StatsBombMatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_season_stats_ClubId",
                table: "player_season_stats",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_player_season_stats_PlayerId",
                table: "player_season_stats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_players_StatsBombPlayerId",
                table: "players",
                column: "StatsBombPlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "player_season_stats");

            migrationBuilder.DropTable(
                name: "clubs");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
