using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniTube.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Videos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Videos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Videos");
        }
    }
}
