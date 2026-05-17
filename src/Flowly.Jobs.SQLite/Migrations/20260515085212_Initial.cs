using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.Jobs.SQLite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomJobStates",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomState = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomJobStates", x => x.JobIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "JobAliveStatuses",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastAliveTimestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAliveStatuses", x => x.JobIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "JobTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobTypeId = table.Column<long>(type: "INTEGER", nullable: false),
                    JobTypeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CurrentState = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Started = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Completed = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FaultReason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    IsRecurringJob = table.Column<bool>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RetryAttempt = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.JobIdentifier);
                    table.ForeignKey(
                        name: "FK_Jobs_JobTypes_JobTypeId",
                        column: x => x.JobTypeId,
                        principalTable: "JobTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobTypeId",
                table: "Jobs",
                column: "JobTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobTypes_Name",
                table: "JobTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomJobStates");

            migrationBuilder.DropTable(
                name: "JobAliveStatuses");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "JobTypes");
        }
    }
}
