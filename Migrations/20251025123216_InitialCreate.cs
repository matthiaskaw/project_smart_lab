using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartlab.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    DataSource = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OriginalFilename = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DeviceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataPoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ParameterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataPoints_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationErrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ErrorType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    RowIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    ParameterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationErrors_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_DatasetId_Timestamp",
                table: "DataPoints",
                columns: new[] { "DatasetId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_ParameterName",
                table: "DataPoints",
                column: "ParameterName");

            migrationBuilder.CreateIndex(
                name: "IX_DataPoints_RowIndex",
                table: "DataPoints",
                column: "RowIndex");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_CreatedDate",
                table: "Datasets",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_DataSource",
                table: "Datasets",
                column: "DataSource");

            migrationBuilder.CreateIndex(
                name: "IX_Datasets_DeviceId",
                table: "Datasets",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigurations_DeviceType",
                table: "DeviceConfigurations",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigurations_IsActive",
                table: "DeviceConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceConfigurations_Name",
                table: "DeviceConfigurations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationErrors_DatasetId",
                table: "ValidationErrors",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationErrors_ErrorType",
                table: "ValidationErrors",
                column: "ErrorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataPoints");

            migrationBuilder.DropTable(
                name: "DeviceConfigurations");

            migrationBuilder.DropTable(
                name: "ValidationErrors");

            migrationBuilder.DropTable(
                name: "Datasets");
        }
    }
}
