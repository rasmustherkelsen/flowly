using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flowly.Jobs.Migrations
{
    /// <inheritdoc />
    public partial class JobsDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomJobStates",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomState = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomJobStates", x => x.JobIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "JobAliveStatuses",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastAliveTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAliveStatuses", x => x.JobIdentifier);
                });

            migrationBuilder.CreateTable(
                name: "JobTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    JobIdentifier = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobTypeId = table.Column<long>(type: "bigint", nullable: false),
                    JobTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Started = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Completed = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FaultReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsRecurringJob = table.Column<bool>(type: "bit", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
