// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    DisplayUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    Culture = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    IsFile = table.Column<bool>(type: "bit", nullable: false),
                    HitCount = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false)
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
