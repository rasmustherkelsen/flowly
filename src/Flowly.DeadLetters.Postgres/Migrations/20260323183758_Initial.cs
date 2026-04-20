using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.DeadLetters.Postgres.Migrations
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
                    MessageId = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QueueName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageBody = table.Column<string>(type: "text", nullable: false),
                    MessageProperties = table.Column<string>(type: "text", nullable: false),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadLetterReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeadLetterErrorDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SubscriptionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequeuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequeuedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
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
