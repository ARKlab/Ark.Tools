using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ODataEntityFrameworkSample.Migrations
{
    public partial class PhotoStudios : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotoStudios",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    AuditId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoStudios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoStudios_Audits_AuditId",
                        column: x => x.AuditId,
                        principalTable: "Audits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Worker",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false),
                    SurName = table.Column<string>(nullable: false),
                    Role = table.Column<int>(nullable: false),
                    Quality = table.Column<string>(nullable: true),
                    PhotoStudioId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Worker", x => new { x.Name, x.SurName, x.Role });
                    table.ForeignKey(
                        name: "FK_Worker_PhotoStudios_PhotoStudioId",
                        column: x => x.PhotoStudioId,
                        principalTable: "PhotoStudios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoStudios_AuditId",
                table: "PhotoStudios",
                column: "AuditId");

            migrationBuilder.CreateIndex(
                name: "IX_Worker_PhotoStudioId",
                table: "Worker",
                column: "PhotoStudioId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Worker");

            migrationBuilder.DropTable(
                name: "PhotoStudios");
        }
    }
}
