// Copyright (c) Dave Woestenborghs and contributors. Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Community.PageNotFoundTracker.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_pageNotFoundTrackerUrl_LastSeen",
                table: "pageNotFoundTrackerUrl",
                column: "LastSeen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pageNotFoundTrackerUrl_LastSeen",
                table: "pageNotFoundTrackerUrl");
        }
    }
}
