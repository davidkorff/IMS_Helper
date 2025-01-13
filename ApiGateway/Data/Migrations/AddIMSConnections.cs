using Microsoft.EntityFrameworkCore.Migrations;
using System;

public partial class AddIMSConnections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IMSConnections",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                UserId = table.Column<string>(nullable: false),
                ConnectionName = table.Column<string>(maxLength: 100, nullable: false),
                Environment = table.Column<string>(maxLength: 20, nullable: false),
                EncryptedCredentials = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastUsedAt = table.Column<DateTime>(nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IMSConnections", x => x.Id);
                table.ForeignKey(
                    name: "FK_IMSConnections_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "IMSConnectionPermissions",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                ConnectionId = table.Column<string>(nullable: false),
                PermissionName = table.Column<string>(maxLength: 100, nullable: false),
                IsGranted = table.Column<bool>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IMSConnectionPermissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_IMSConnectionPermissions_IMSConnections_ConnectionId",
                    column: x => x.ConnectionId,
                    principalTable: "IMSConnections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IMSConnections_UserId",
            table: "IMSConnections",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_IMSConnectionPermissions_ConnectionId_PermissionName",
            table: "IMSConnectionPermissions",
            columns: new[] { "ConnectionId", "PermissionName" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IMSConnectionPermissions");
        migrationBuilder.DropTable(name: "IMSConnections");
    }
} 