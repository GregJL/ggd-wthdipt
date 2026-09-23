using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ggd.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEditLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEditLocked",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEditLocked",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEditLocked",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsEditLocked",
                table: "Items");
        }
    }
}
