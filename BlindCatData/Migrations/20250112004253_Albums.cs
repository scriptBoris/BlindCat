using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindCatData.Migrations
{
    /// <inheritdoc />
    public partial class Albums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Contents",
                table: "Contents");

            migrationBuilder.RenameTable(
                name: "Contents",
                newName: "ContentStorageDb");

            migrationBuilder.AddColumn<Guid>(
                name: "Parent",
                table: "ContentStorageDb",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ContentStorageDb",
                table: "ContentStorageDb",
                column: "Guid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ContentStorageDb",
                table: "ContentStorageDb");

            migrationBuilder.DropColumn(
                name: "Parent",
                table: "ContentStorageDb");

            migrationBuilder.RenameTable(
                name: "ContentStorageDb",
                newName: "Contents");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Contents",
                table: "Contents",
                column: "Guid");
        }
    }
}
