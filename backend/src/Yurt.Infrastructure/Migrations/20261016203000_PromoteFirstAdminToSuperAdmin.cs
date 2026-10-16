using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Yurt.Infrastructure.Persistence;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <summary>Data-only migration: introduces the SuperAdmin role by promoting the original
    /// (earliest-created) Admin account, so the existing owner keeps access to IT sections.
    /// No-op when a SuperAdmin already exists. Role is stored as text, so no schema change.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016203000_PromoteFirstAdminToSuperAdmin")]
    public partial class PromoteFirstAdminToSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AdminUsers" SET "Role" = 'SuperAdmin'
                WHERE "Id" = (
                    SELECT "Id" FROM "AdminUsers"
                    WHERE "Role" = 'Admin'
                    ORDER BY "CreatedAt"
                    LIMIT 1)
                AND NOT EXISTS (SELECT 1 FROM "AdminUsers" WHERE "Role" = 'SuperAdmin');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "AdminUsers" SET "Role" = 'Admin' WHERE "Role" = 'SuperAdmin';""");
        }
    }
}
