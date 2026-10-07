using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dbvprovas.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clubs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clubs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_memberships_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memberships_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_person_id",
                table: "accounts",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_club_id",
                table: "memberships",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_person_id",
                table: "memberships",
                column: "person_id");
            // Segunda barreira do isolamento (RN-TEN-001, RNF-TEN-001, D-121, D-124): o RLS lê o contexto
            // que o ClubSessionInterceptor grava na sessão. O app (dbv_app) não é dono das tabelas.
            migrationBuilder.Sql("""
                ALTER TABLE memberships ENABLE ROW LEVEL SECURITY;
                CREATE POLICY memberships_select ON memberships FOR SELECT
                    USING (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid
                        OR person_id = NULLIF(current_setting('app.person_id', true), '')::uuid);
                CREATE POLICY memberships_insert ON memberships FOR INSERT
                    WITH CHECK (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid);
                CREATE POLICY memberships_update ON memberships FOR UPDATE
                    USING (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid)
                    WITH CHECK (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid);
                CREATE POLICY memberships_delete ON memberships FOR DELETE
                    USING (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid);

                ALTER TABLE persons ENABLE ROW LEVEL SECURITY;
                CREATE POLICY persons_select ON persons FOR SELECT
                    USING (id = NULLIF(current_setting('app.person_id', true), '')::uuid
                        OR EXISTS (SELECT 1 FROM memberships m
                                   WHERE m.person_id = persons.id
                                     AND m.club_id = NULLIF(current_setting('app.club_id', true), '')::uuid));

                GRANT SELECT ON clubs, accounts, persons TO dbv_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON memberships TO dbv_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "clubs");

            migrationBuilder.DropTable(
                name: "persons");
        }
    }
}
