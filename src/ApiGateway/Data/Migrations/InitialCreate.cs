using Microsoft.EntityFrameworkCore.Migrations;
using System;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UsageRecords",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ApiKey = table.Column<string>(maxLength: 100, nullable: false),
                Endpoint = table.Column<string>(maxLength: 255, nullable: false),
                StatusCode = table.Column<int>(nullable: false),
                Timestamp = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsageRecords", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UsageRecords_ApiKey",
            table: "UsageRecords",
            column: "ApiKey");

        migrationBuilder.CreateIndex(
            name: "IX_UsageRecords_Timestamp",
            table: "UsageRecords",
            column: "Timestamp");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UsageRecords");
    }
} 