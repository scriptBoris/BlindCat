using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindCatData.Migrations
{
    /// <inheritdoc />
    public partial class AddedcolumnOriginFileSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OriginFileSize",
                table: "Contents",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginFileSize",
                table: "Contents");
        }
    }
}
