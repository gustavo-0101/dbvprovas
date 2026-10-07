using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dbvprovas.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IsolateClubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RNF-TEN-001, D-124
            migrationBuilder.Sql("""
                ALTER TABLE clubs ENABLE ROW LEVEL SECURITY;
                CREATE POLICY clubs_select ON clubs FOR SELECT
                    USING (id = NULLIF(current_setting('app.club_id', true), '')::uuid
                        OR (NULLIF(current_setting('app.club_id', true), '') IS NULL
                            AND EXISTS (SELECT 1 FROM memberships m
                                        WHERE m.club_id = clubs.id
                                          AND m.person_id = NULLIF(current_setting('app.person_id', true), '')::uuid
                                          AND m.ended_at IS NULL)));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY clubs_select ON clubs;
                ALTER TABLE clubs DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
