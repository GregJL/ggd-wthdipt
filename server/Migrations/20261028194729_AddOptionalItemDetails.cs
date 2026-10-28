using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ggd.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalItemDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "Items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PurchaseDate",
                table: "Items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                table: "Items",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementValue",
                table: "Items",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "Tags",
                table: "Items",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyExpiresOn",
                table: "Items",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarrantyNotes",
                table: "Items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ReplacementValue",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "WarrantyExpiresOn",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "WarrantyNotes",
                table: "Items");
        }
    }
}
