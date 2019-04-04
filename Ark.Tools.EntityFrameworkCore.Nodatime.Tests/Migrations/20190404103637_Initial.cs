using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

namespace Ark.Tools.EntityFrameworkCore.Nodatime.Tests.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntityAs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityAs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityBs",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Address_Street = table.Column<string>(nullable: true),
                    Address_City = table.Column<string>(nullable: true),
                    LocalDate = table.Column<LocalDate>(nullable: false),
                    LocalDateTime = table.Column<LocalDateTime>(nullable: false),
                    Instant = table.Column<Instant>(nullable: false),
                    OffsetDateTime = table.Column<OffsetDateTime>(nullable: false),
                    DateTime = table.Column<DateTime>(nullable: false),
                    DateTimeOffset = table.Column<DateTimeOffset>(nullable: false),
                    TimeSpan = table.Column<TimeSpan>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityBs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityAs_Addresses",
                columns: table => new
                {
                    EntityAId = table.Column<int>(nullable: false),
                    Id = table.Column<int>(nullable: false),
                    Street = table.Column<string>(nullable: true),
                    City = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityAs_Addresses", x => new { x.EntityAId, x.Id });
                    table.ForeignKey(
                        name: "FK_EntityAs_Addresses_EntityAs_EntityAId",
                        column: x => x.EntityAId,
                        principalTable: "EntityAs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntityAs_Addresses");

            migrationBuilder.DropTable(
                name: "EntityBs");

            migrationBuilder.DropTable(
                name: "EntityAs");
        }
    }
}
