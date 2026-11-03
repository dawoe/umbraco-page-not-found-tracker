// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.PageNotFoundTracker.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pageNotFoundTrackerUrl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    UrlHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 800, nullable: false),
                    DisplayUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, defaultValue: ""),
                    Culture = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    IsFile = table.Column<bool>(type: "INTEGER", nullable: false),
                    HitCount = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pageNotFoundTrackerUrl", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pageNotFoundTrackerUrl_RelativePath",
                table: "pageNotFoundTrackerUrl",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_pageNotFoundTrackerUrl_UrlHash_Domain_Culture",
                table: "pageNotFoundTrackerUrl",
                columns: new[] { "UrlHash", "Domain", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pageNotFoundTrackerUrl");
        }
    }
}
