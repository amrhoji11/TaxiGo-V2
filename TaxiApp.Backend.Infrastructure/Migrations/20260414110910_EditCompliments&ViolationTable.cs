using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxiApp.Backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EditComplimentsViolationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Complaints",
                newName: "TargetType");

            migrationBuilder.AddColumn<int>(
                name: "ReasonType",
                table: "Complaints",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasonType",
                table: "Complaints");

            migrationBuilder.RenameColumn(
                name: "TargetType",
                table: "Complaints",
                newName: "Type");
        }
    }
}
