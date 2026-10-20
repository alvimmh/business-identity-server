using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessIdentityServer.Internals.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateEmailRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SentFrom = table.Column<string>(type: "text", nullable: false),
                    SentTo = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResentAt = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<string>(type: "text", nullable: true),
                    SendAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailRecords");
        }
    }
}
