using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.DeadLetters.SQLite.Migrations
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
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    QueueName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubscriptionName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MessageBody = table.Column<string>(type: "TEXT", nullable: false),
                    MessageProperties = table.Column<string>(type: "TEXT", nullable: false),
                    DeadLetteredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeadLetterReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DeadLetterErrorDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RequeuedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RequeuedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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
