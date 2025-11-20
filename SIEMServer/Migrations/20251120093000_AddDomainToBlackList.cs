using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIEMServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainToBlackList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "BlacklistedProcesses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domain",
                table: "BlacklistedProcesses");
        }
    }
}
