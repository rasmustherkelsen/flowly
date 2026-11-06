using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.DeadLetters.SqlServer.Migrations
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
                type: "nvarchar(max)",
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
