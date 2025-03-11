using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmailQueuePOC.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "EmailQueue",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "EmailQueue");
        }
    }
}
