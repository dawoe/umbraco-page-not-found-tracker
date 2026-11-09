// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pageNotFoundTrackerBlockRule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    PatternHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayPattern = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, defaultValue: ""),
                    Culture = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pageNotFoundTrackerBlockRule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pageNotFoundTrackerBlockRule_PatternHash_Domain_Culture",
                table: "pageNotFoundTrackerBlockRule",
                columns: new[] { "PatternHash", "Domain", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pageNotFoundTrackerBlockRule");
        }
    }
}
