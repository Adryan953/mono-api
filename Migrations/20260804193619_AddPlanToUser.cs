using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mono.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plano",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plano",
                table: "Users");
        }
    }
}
