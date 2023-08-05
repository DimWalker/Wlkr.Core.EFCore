using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EFCoreSample.Migrations
{
    public partial class _0001 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Str",
                table: "TestModel",
                newName: "S");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "S",
                table: "TestModel",
                newName: "Str");
        }
    }
}
