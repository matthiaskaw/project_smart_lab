using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartlab.Migrations
{
    /// <inheritdoc />
    public partial class AddRawDataJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawDataJson",
                table: "Datasets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawDataJson",
                table: "Datasets");
        }
    }
}
