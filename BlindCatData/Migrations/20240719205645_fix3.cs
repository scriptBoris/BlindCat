using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindCatData.Migrations
{
    /// <inheritdoc />
    public partial class fix3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateIndex",
                table: "Contents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateLastIndex",
                table: "Contents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateIndex",
                table: "Contents");

            migrationBuilder.DropColumn(
                name: "DateLastIndex",
                table: "Contents");
        }
    }
}
