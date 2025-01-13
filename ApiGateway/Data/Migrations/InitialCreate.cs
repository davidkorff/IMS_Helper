using Microsoft.EntityFrameworkCore.Migrations;
using System;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create ASP.NET Identity tables first
        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(nullable: false),
                UserName = table.Column<string>(maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(maxLength: 256, nullable: true),
                Email = table.Column<string>(maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(nullable: false),
                PasswordHash = table.Column<string>(nullable: true),
                SecurityStamp = table.Column<string>(nullable: true),
                ConcurrencyStamp = table.Column<string>(nullable: true),
                PhoneNumber = table.Column<string>(nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(nullable: false),
                TwoFactorEnabled = table.Column<bool>(nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(nullable: true),
                LockoutEnabled = table.Column<bool>(nullable: false),
                AccessFailedCount = table.Column<int>(nullable: false),
                FirstName = table.Column<string>(maxLength: 50, nullable: true),
                LastName = table.Column<string>(maxLength: 50, nullable: true),
                CompanyName = table.Column<string>(maxLength: 100, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                LastLoginAt = table.Column<DateTime>(nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                TimeZone = table.Column<string>(maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        // Add other Identity tables...

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
                IsActive = table.Column<bool>(nullable: false)
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

        // Add indexes
        migrationBuilder.CreateIndex(
            name: "IX_IMSConnections_UserId",
            table: "IMSConnections",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IMSConnections");
        migrationBuilder.DropTable(name: "AspNetUsers");
        // Drop other tables...
    }
} 