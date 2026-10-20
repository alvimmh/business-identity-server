using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessIdentityServer.Internals.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateUserAccessRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccessRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    AccessorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessRecords_AccessorId",
                table: "UserAccessRecords",
                column: "AccessorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessRecords_UserId",
                table: "UserAccessRecords",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAccessRecords");
        }
    }
}
