using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ggd.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "Photos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsStaging",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsSorting",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PlannedLocationId",
                table: "Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceName = table.Column<string>(type: "text", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_PlannedLocationId",
                table: "Items",
                column: "PlannedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationTasks_WorkspaceId_IsCompleted",
                table: "OrganizationTasks",
                columns: new[] { "WorkspaceId", "IsCompleted" });

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Locations_PlannedLocationId",
                table: "Items",
                column: "PlannedLocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Locations_PlannedLocationId",
                table: "Items");

            migrationBuilder.DropTable(
                name: "OrganizationTasks");

            migrationBuilder.DropIndex(
                name: "IX_Items_PlannedLocationId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "IsStaging",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "NeedsSorting",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PlannedLocationId",
                table: "Items");
        }
    }
}
