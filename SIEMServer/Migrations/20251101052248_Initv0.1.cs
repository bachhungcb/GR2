using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIEMServer.Migrations
{
    /// <inheritdoc />
    public partial class Initv01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentIpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snapshot_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEndPointAddr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalEndPointName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemoteEndPointAddr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemoteEndPointName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionEntries_Snapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Pid = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessEntries_Snapshot_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshot",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionEntries_SnapshotId",
                table: "ConnectionEntries",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEntries_SnapshotId",
                table: "ProcessEntries",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshot_AgentId",
                table: "Snapshot",
                column: "AgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionEntries");

            migrationBuilder.DropTable(
                name: "ProcessEntries");

            migrationBuilder.DropTable(
                name: "Snapshot");

            migrationBuilder.DropTable(
                name: "Agents");
        }
    }
}
