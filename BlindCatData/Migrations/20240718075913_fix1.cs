using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindCatData.Migrations
{
    /// <inheritdoc />
    public partial class fix1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Meta",
                keyColumn: "Id",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Meta",
                columns: new[] { "Id", "Key", "Value" },
                values: new object[] { 1, "MGE_jtov", "Data can be decrypted!" });
        }
    }
}
