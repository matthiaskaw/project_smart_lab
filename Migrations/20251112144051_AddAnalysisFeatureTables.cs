using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace smartlab.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisFeatureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScriptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScriptName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ScriptLanguage = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScriptVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ExecutionDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: true),
                    ResultImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ResultDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutionTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ScriptContentSnapshot = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisResults_Datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "Datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScriptMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    UploadDate = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')"),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ValidationErrorsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationWarningsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsShared = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastExecuted = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptMetadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_DatasetId",
                table: "AnalysisResults",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_ExecutionDate",
                table: "AnalysisResults",
                column: "ExecutionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_ScriptId",
                table: "AnalysisResults",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_Status",
                table: "AnalysisResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMetadata_IsBuiltIn",
                table: "ScriptMetadata",
                column: "IsBuiltIn");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMetadata_IsShared",
                table: "ScriptMetadata",
                column: "IsShared");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMetadata_Language",
                table: "ScriptMetadata",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMetadata_UserId",
                table: "ScriptMetadata",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMetadata_ValidationStatus",
                table: "ScriptMetadata",
                column: "ValidationStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisResults");

            migrationBuilder.DropTable(
                name: "ScriptMetadata");
        }
    }
}
