using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ggd.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementAndLocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMoveLocked",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMoveLocked",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ItemMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemMovements_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToParentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationMovements_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemMovements_ItemId_MovedAt",
                table: "ItemMovements",
                columns: new[] { "ItemId", "MovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationMovements_LocationId_MovedAt",
                table: "LocationMovements",
                columns: new[] { "LocationId", "MovedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemMovements");

            migrationBuilder.DropTable(
                name: "LocationMovements");

            migrationBuilder.DropColumn(
                name: "IsMoveLocked",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsMoveLocked",
                table: "Items");
        }
    }
}
