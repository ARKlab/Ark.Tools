using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

namespace ODataSample.Migrations
{
    public partial class InitSample : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audits",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(nullable: false),
                    UserId = table.Column<string>(nullable: true),
                    Timestamp = table.Column<Instant>(nullable: false, computedColumnSql: "[SysStartTime]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audits", x => x.AuditId);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Press",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    Email = table.Column<string>(nullable: true),
                    Category = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Press", x => x.Id);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "AffectedEntity",
                columns: table => new
                {
                    EntityId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    AuditId = table.Column<Guid>(nullable: false),
                    TableName = table.Column<string>(nullable: true),
                    EntityAction = table.Column<string>(nullable: true),
                    KeyValues = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffectedEntity", x => x.EntityId);
                    table.ForeignKey(
                        name: "FK_AffectedEntity_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "AuditId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ISBN = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true),
                    Author = table.Column<string>(nullable: true),
                    Price = table.Column<decimal>(nullable: false),
                    Location_City = table.Column<string>(nullable: true),
                    Location_Street = table.Column<string>(nullable: true),
                    AuditId = table.Column<Guid>(nullable: false),
                    PressId = table.Column<int>(nullable: true),
                    _ETag = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Books_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "AuditId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Books_Press_PressId",
                        column: x => x.PressId,
                        principalTable: "Press",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Books_Addresses",
                columns: table => new
                {
                    BookId = table.Column<int>(nullable: false),
                    Id = table.Column<int>(nullable: false),
                    City = table.Column<string>(nullable: true),
                    Street = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books_Addresses", x => new { x.BookId, x.Id });
                    table.ForeignKey(
                        name: "FK_Books_Addresses_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateIndex(
                name: "IX_AffectedEntity_AuditId",
                table: "AffectedEntity",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_AuditId",
                table: "Books",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_PressId",
                table: "Books",
                column: "PressId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffectedEntity");

            migrationBuilder.DropTable(
                name: "Books_Addresses");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "Audits");

            migrationBuilder.DropTable(
                name: "Press");
        }
    }
}
