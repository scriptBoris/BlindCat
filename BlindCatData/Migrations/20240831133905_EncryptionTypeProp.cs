using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlindCatData.Migrations
{
    /// <inheritdoc />
    public partial class EncryptionTypeProp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionType",
                table: "Contents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Contents
                SET EncryptionType = 'UNSET'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionType",
                table: "Contents");
        }
    }
}
