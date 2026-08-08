using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mono.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPlanExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataExpiracaoPlano",
                table: "Users",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataExpiracaoPlano",
                table: "Users");
        }
    }
}
