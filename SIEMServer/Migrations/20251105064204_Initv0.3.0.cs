using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIEMServer.Migrations
{
    /// <inheritdoc />
    public partial class Initv030 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Commandline",
                table: "ProcessEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "ProcessEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Commandline",
                table: "ProcessEntries");

            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "ProcessEntries");
        }
    }
}
