using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.DeadLetters.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeadLetters",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    QueueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeadLetterReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeadLetterErrorDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequeuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequeuedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadLetters", x => x.MessageId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeadLetters");
        }
    }
}
