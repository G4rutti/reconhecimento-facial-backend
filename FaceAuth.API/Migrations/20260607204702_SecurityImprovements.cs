using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceAuth.API.Migrations
{
    /// <inheritdoc />
    public partial class SecurityImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "Embeddings",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Embeddings",
                table: "Users");
        }
    }
}
