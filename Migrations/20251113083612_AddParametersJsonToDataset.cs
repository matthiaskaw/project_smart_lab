using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartlab.Migrations
{
    /// <inheritdoc />
    public partial class AddParametersJsonToDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParametersJson",
                table: "Datasets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParametersJson",
                table: "Datasets");
        }
    }
}
