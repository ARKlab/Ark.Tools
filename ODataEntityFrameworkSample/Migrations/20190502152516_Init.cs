using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ODataEntityFrameworkSample.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AuditId",
                table: "Audits",
                newName: "Id");

            migrationBuilder.CreateTable(
                name: "Bibliography",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    BookId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bibliography", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bibliography_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<string>(nullable: false),
                    Value = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateTable(
                name: "Code",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Value = table.Column<int>(nullable: false),
                    BibliographyId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Code", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Code_Bibliography_BibliographyId",
                        column: x => x.BibliographyId,
                        principalTable: "Bibliography",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("SystemVersioning", true);

            migrationBuilder.CreateIndex(
                name: "IX_Bibliography_BookId",
                table: "Bibliography",
                column: "BookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Code_BibliographyId",
                table: "Code",
                column: "BibliographyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Code");

            migrationBuilder.DropTable(
                name: "Tests");

            migrationBuilder.DropTable(
                name: "Bibliography");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Audits",
                newName: "AuditId");
        }
    }
}
