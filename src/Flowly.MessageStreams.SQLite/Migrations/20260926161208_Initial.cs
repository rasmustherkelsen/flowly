using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.MessageStreams.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QueueName = table.Column<string>(type: "TEXT", nullable: false),
                    ConsumerName = table.Column<string>(type: "TEXT", nullable: false),
                    Partition = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_QueueName_ConsumerName_Partition",
                table: "Checkpoints",
                columns: new[] { "QueueName", "ConsumerName", "Partition" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Checkpoints");
        }
    }
}
