using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.DeadLetters.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class AddReplacementMessageBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacementMessageBody",
                table: "DeadLetters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacementMessageBody",
                table: "DeadLetters");
        }
    }
}
