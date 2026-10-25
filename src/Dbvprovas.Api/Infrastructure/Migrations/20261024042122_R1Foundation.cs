using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dbvprovas.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class R1Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "persons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_date",
                table: "persons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "persons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "sex",
                table: "persons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "memberships",
                type: "text",
                nullable: false,
                defaultValue: "Leader");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "memberships",
                type: "text",
                nullable: false,
                defaultValue: "Active");
            migrationBuilder.Sql("UPDATE memberships SET status = 'Ended' WHERE ended_at IS NOT NULL;");

            migrationBuilder.AddColumn<string>(
                name: "status_cause",
                table: "memberships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                table: "memberships",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                table: "memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "church",
                table: "clubs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "clubs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "clubs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "founding_request_id",
                table: "clubs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invite_code",
                table: "clubs",
                type: "text",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("UPDATE clubs SET invite_code = translate(upper(substr(md5(id::text), 1, 8)), '01', 'XY');");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_subject",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql("UPDATE accounts SET google_subject = 'r0-dev-' || id::text;");

            migrationBuilder.AddColumn<bool>(
                name: "is_platform_admin",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_memberships_club_id_id",
                table: "memberships",
                columns: new[] { "club_id", "id" });

            migrationBuilder.CreateTable(
                name: "admin_access_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: false),
                    target_membership_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    fields = table.Column<string[]>(type: "text[]", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_access_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_access_requests_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "text", nullable: false),
                    actor_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    cause = table.Column<string>(type: "text", nullable: true),
                    admin_access_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fields = table.Column<string[]>(type: "text[]", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "director_nominations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nominator_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nominee_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_director_nominations", x => x.id);
                    table.ForeignKey(
                        name: "FK_director_nominations_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_director_nominations_memberships_club_id_nominator_membersh~",
                        columns: x => new { x.club_id, x.nominator_membership_id },
                        principalTable: "memberships",
                        principalColumns: new[] { "club_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_director_nominations_memberships_club_id_nominee_membership~",
                        columns: x => new { x.club_id, x.nominee_membership_id },
                        principalTable: "memberships",
                        principalColumns: new[] { "club_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "text", nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "founding_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_founding_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_founding_records_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "founding_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_name = table.Column<string>(type: "text", nullable: false),
                    club_city = table.Column<string>(type: "text", nullable: false),
                    club_church = table.Column<string>(type: "text", nullable: true),
                    proposed_club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    exception_requested = table.Column<bool>(type: "boolean", nullable: false),
                    exception_granted = table.Column<bool>(type: "boolean", nullable: false),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    decided_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_founding_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_founding_requests_accounts_requester_account_id",
                        column: x => x.requester_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_founding_requests_persons_requester_person_id",
                        column: x => x.requester_person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @event = table.Column<string>(name: "event", type: "text", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: true),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_persons_recipient_person_id",
                        column: x => x.recipient_person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    nonce_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.CheckConstraint("ck_sessions_target", "target IN ('Web', 'App')");
                    table.ForeignKey(
                        name: "FK_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sex = table.Column<string>(type: "text", nullable: false),
                    min_age = table.Column<int>(type: "integer", nullable: false),
                    max_age = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                    table.UniqueConstraint("AK_units_club_id_id", x => new { x.club_id, x.id });
                    table.CheckConstraint("ck_units_age_range", "min_age <= max_age AND min_age BETWEEN 10 AND 15 AND max_age BETWEEN 10 AND 15");
                    table.CheckConstraint("ck_units_sex", "sex IN ('Female', 'Male')");
                    table.ForeignKey(
                        name: "FK_units_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: true),
                    founding_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    term_version = table.Column<string>(type: "text", nullable: false),
                    consented_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_cause = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consents", x => x.id);
                    table.CheckConstraint("ck_consents_context", "club_id IS NOT NULL OR founding_request_id IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_consents_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_consents_founding_requests_founding_request_id",
                        column: x => x.founding_request_id,
                        principalTable: "founding_requests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_consents_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    end_cause = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.id);
                    table.CheckConstraint("ck_role_assignments_role", "role IN ('Director', 'AssociateDirector', 'Secretary', 'Treasurer', 'Chaplain', 'Pastor', 'Counselor', 'AssociateCounselor', 'Instructor')");
                    table.CheckConstraint("ck_role_assignments_unit", "(role IN ('Counselor', 'AssociateCounselor')) = (unit_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_role_assignments_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_assignments_memberships_club_id_membership_id",
                        columns: x => new { x.club_id, x.membership_id },
                        principalTable: "memberships",
                        principalColumns: new[] { "club_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_assignments_units_club_id_unit_id",
                        columns: x => new { x.club_id, x.unit_id },
                        principalTable: "units",
                        principalColumns: new[] { "club_id", "id" });
                });

            migrationBuilder.CreateTable(
                name: "join_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    club_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    consent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    close_cause = table.Column<string>(type: "text", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_join_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_join_requests_accounts_created_by_account_id",
                        column: x => x.created_by_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_requests_clubs_club_id",
                        column: x => x.club_id,
                        principalTable: "clubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_requests_consents_consent_id",
                        column: x => x.consent_id,
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_requests_persons_candidate_person_id",
                        column: x => x.candidate_person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_persons_sex",
                table: "persons",
                sql: "sex IS NULL OR sex IN ('Female', 'Male')");

            migrationBuilder.CreateIndex(
                name: "ux_memberships_one_active_per_person",
                table: "memberships",
                column: "person_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_ended",
                table: "memberships",
                sql: "(status = 'Ended') = (ended_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_suspended",
                table: "memberships",
                sql: "(status = 'Suspended') = (suspended_at IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_clubs_invite_code",
                table: "clubs",
                column: "invite_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_accounts_google_subject",
                table: "accounts",
                column: "google_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_access_requests_club_id",
                table: "admin_access_requests",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_club_id_occurred_at",
                table: "audit_entries",
                columns: new[] { "club_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_scope_occurred_at",
                table: "audit_entries",
                columns: new[] { "scope", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_consents_club_id",
                table: "consents",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "IX_consents_founding_request_id",
                table: "consents",
                column: "founding_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_consents_person_id",
                table: "consents",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ux_consents_one_active_per_club",
                table: "consents",
                columns: new[] { "person_id", "club_id" },
                unique: true,
                filter: "revoked_at IS NULL AND ended_at IS NULL AND club_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_director_nominations_club_id_nominator_membership_id",
                table: "director_nominations",
                columns: new[] { "club_id", "nominator_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_director_nominations_club_id_nominee_membership_id",
                table: "director_nominations",
                columns: new[] { "club_id", "nominee_membership_id" });

            migrationBuilder.CreateIndex(
                name: "ux_director_nominations_one_pending",
                table: "director_nominations",
                column: "club_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_email_outbox_once",
                table: "email_outbox",
                columns: new[] { "template", "object_id", "recipient_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_founding_records_person_id",
                table: "founding_records",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_founding_requests_proposed_club_id",
                table: "founding_requests",
                column: "proposed_club_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_founding_requests_requester_account_id",
                table: "founding_requests",
                column: "requester_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_founding_requests_one_pending_per_person",
                table: "founding_requests",
                column: "requester_person_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_candidate_person_id",
                table: "join_requests",
                column: "candidate_person_id");

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_club_id_status",
                table: "join_requests",
                columns: new[] { "club_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_consent_id",
                table: "join_requests",
                column: "consent_id");

            migrationBuilder.CreateIndex(
                name: "IX_join_requests_created_by_account_id",
                table: "join_requests",
                column: "created_by_account_id");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_once",
                table: "notifications",
                columns: new[] { "recipient_person_id", "event", "object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_club_id",
                table: "role_assignments",
                column: "club_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_club_id_membership_id",
                table: "role_assignments",
                columns: new[] { "club_id", "membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_club_id_unit_id",
                table: "role_assignments",
                columns: new[] { "club_id", "unit_id" });

            migrationBuilder.CreateIndex(
                name: "ux_role_assignments_one_per_membership_role",
                table: "role_assignments",
                columns: new[] { "membership_id", "role" },
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_role_assignments_unique_club_roles",
                table: "role_assignments",
                columns: new[] { "club_id", "role" },
                unique: true,
                filter: "ended_at IS NULL AND role IN ('Director', 'Secretary', 'Treasurer')");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_account_id",
                table: "sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ux_sessions_nonce_hash",
                table: "sessions",
                column: "nonce_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sessions_token_hash",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_club_id",
                table: "units",
                column: "club_id");

            // Segunda barreira do R1 (RN-TEN-001, RNF-TEN-006, D-121, D-124). O RLS lê o contexto que o
            // ClubSessionInterceptor grava na sessão. O app (dbv_app) não é dono das tabelas nem das visões.
            migrationBuilder.Sql("""
                CREATE FUNCTION app_club_id() RETURNS uuid LANGUAGE sql STABLE
                    AS $$ SELECT NULLIF(current_setting('app.club_id', true), '')::uuid $$;
                CREATE FUNCTION app_person_id() RETURNS uuid LANGUAGE sql STABLE
                    AS $$ SELECT NULLIF(current_setting('app.person_id', true), '')::uuid $$;
                CREATE FUNCTION app_is_platform_admin() RETURNS boolean LANGUAGE sql STABLE
                    AS $$ SELECT coalesce(current_setting('app.platform_admin', true), '') = 'true' $$;

                DROP POLICY clubs_select ON clubs;
                CREATE POLICY clubs_select ON clubs FOR SELECT
                    USING (id = app_club_id()
                        OR (app_club_id() IS NULL AND EXISTS (SELECT 1 FROM memberships m
                            WHERE m.club_id = clubs.id AND m.person_id = app_person_id() AND m.status = 'Active')));
                -- Clube só nasce pela aprovação da fundação, com o papel de admin (RN-TEN-002, CA-TEN-010).
                CREATE POLICY clubs_insert ON clubs FOR INSERT WITH CHECK (id = app_club_id() AND app_is_platform_admin());
                CREATE POLICY clubs_update ON clubs FOR UPDATE USING (id = app_club_id()) WITH CHECK (id = app_club_id());

                DROP POLICY persons_select ON persons;
                CREATE POLICY persons_select ON persons FOR SELECT
                    USING (id = app_person_id()
                        OR EXISTS (SELECT 1 FROM memberships m WHERE m.person_id = persons.id AND m.club_id = app_club_id())
                        OR EXISTS (SELECT 1 FROM join_requests j WHERE j.candidate_person_id = persons.id AND j.club_id = app_club_id())
                        OR (app_is_platform_admin() AND EXISTS (SELECT 1 FROM founding_requests f
                            WHERE f.requester_person_id = persons.id AND f.status = 'Pending')));
                CREATE POLICY persons_insert ON persons FOR INSERT WITH CHECK (id = app_person_id());
                CREATE POLICY persons_update ON persons FOR UPDATE USING (id = app_person_id()) WITH CHECK (id = app_person_id());

                ALTER TABLE units ENABLE ROW LEVEL SECURITY;
                CREATE POLICY units_select ON units FOR SELECT USING (club_id = app_club_id());
                CREATE POLICY units_insert ON units FOR INSERT WITH CHECK (club_id = app_club_id());
                CREATE POLICY units_update ON units FOR UPDATE USING (club_id = app_club_id()) WITH CHECK (club_id = app_club_id());

                ALTER TABLE role_assignments ENABLE ROW LEVEL SECURITY;
                CREATE POLICY role_assignments_select ON role_assignments FOR SELECT
                    USING (club_id = app_club_id()
                        OR EXISTS (SELECT 1 FROM memberships m WHERE m.id = role_assignments.membership_id AND m.person_id = app_person_id()));
                CREATE POLICY role_assignments_insert ON role_assignments FOR INSERT WITH CHECK (club_id = app_club_id());
                CREATE POLICY role_assignments_update ON role_assignments FOR UPDATE
                    USING (club_id = app_club_id()) WITH CHECK (club_id = app_club_id());

                ALTER TABLE director_nominations ENABLE ROW LEVEL SECURITY;
                CREATE POLICY director_nominations_select ON director_nominations FOR SELECT USING (club_id = app_club_id());
                CREATE POLICY director_nominations_insert ON director_nominations FOR INSERT WITH CHECK (club_id = app_club_id());
                CREATE POLICY director_nominations_update ON director_nominations FOR UPDATE
                    USING (club_id = app_club_id()) WITH CHECK (club_id = app_club_id());

                ALTER TABLE join_requests ENABLE ROW LEVEL SECURITY;
                CREATE POLICY join_requests_select ON join_requests FOR SELECT
                    USING (club_id = app_club_id() OR candidate_person_id = app_person_id());
                CREATE POLICY join_requests_insert ON join_requests FOR INSERT WITH CHECK (club_id = app_club_id());
                CREATE POLICY join_requests_update ON join_requests FOR UPDATE
                    USING (club_id = app_club_id()) WITH CHECK (club_id = app_club_id());
                CREATE POLICY join_requests_delete ON join_requests FOR DELETE USING (club_id = app_club_id());

                ALTER TABLE consents ENABLE ROW LEVEL SECURITY;
                CREATE POLICY consents_select ON consents FOR SELECT
                    USING (person_id = app_person_id() OR club_id = app_club_id() OR (club_id IS NULL AND app_is_platform_admin()));
                CREATE POLICY consents_insert ON consents FOR INSERT
                    WITH CHECK (person_id = app_person_id() AND (club_id IS NULL OR club_id = app_club_id()));
                -- Só o titular muda o próprio consentimento; o admin, só o pré-fundação (com founding_request_id), ao decidir
                -- (CA-PRV-008, CA-TEN-010). O clube fica no USING porque o SELECT ... FOR SHARE da diretoria (RowLocks) também
                -- passa por ele; quem barra a escrita da diretoria e a do admin no consentimento de um membro é o WITH CHECK.
                CREATE POLICY consents_update ON consents FOR UPDATE
                    USING (person_id = app_person_id() OR club_id = app_club_id() OR (club_id IS NULL AND app_is_platform_admin()))
                    WITH CHECK ((club_id IS NULL OR club_id = app_club_id())
                        AND (person_id = app_person_id() OR (app_is_platform_admin() AND founding_request_id IS NOT NULL)));
                -- O consentimento do pedido de entrada é do mesmo candidato e do mesmo clube (RN-JOIN-004, RN-TEN-001).
                CREATE UNIQUE INDEX ux_consents_id_club_person ON consents (id, club_id, person_id);
                ALTER TABLE join_requests ADD CONSTRAINT fk_join_requests_consent_same_club_person
                    FOREIGN KEY (consent_id, club_id, candidate_person_id) REFERENCES consents (id, club_id, person_id);

                ALTER TABLE founding_requests ENABLE ROW LEVEL SECURITY;
                CREATE POLICY founding_requests_select ON founding_requests FOR SELECT
                    USING (requester_person_id = app_person_id() OR app_is_platform_admin());
                CREATE POLICY founding_requests_insert ON founding_requests FOR INSERT WITH CHECK (requester_person_id = app_person_id());
                -- O solicitante só cancela o próprio pedido ainda pendente; quem decide é o admin (RN-TEN-002, CA-TEN-009, CA-TEN-010).
                CREATE POLICY founding_requests_update ON founding_requests FOR UPDATE
                    USING ((requester_person_id = app_person_id() AND status = 'Pending') OR app_is_platform_admin())
                    WITH CHECK (app_is_platform_admin() OR (requester_person_id = app_person_id() AND status = 'Cancelled'));

                ALTER TABLE founding_records ENABLE ROW LEVEL SECURITY;
                CREATE POLICY founding_records_select ON founding_records FOR SELECT
                    USING (person_id = app_person_id() OR app_is_platform_admin());
                CREATE POLICY founding_records_insert ON founding_records FOR INSERT WITH CHECK (app_is_platform_admin());

                ALTER TABLE admin_access_requests ENABLE ROW LEVEL SECURITY;
                CREATE POLICY admin_access_requests_select ON admin_access_requests FOR SELECT
                    USING (club_id = app_club_id() OR app_is_platform_admin());
                CREATE POLICY admin_access_requests_insert ON admin_access_requests FOR INSERT
                    WITH CHECK (club_id = app_club_id() AND app_is_platform_admin());
                -- Quem autoriza, nega ou revoga é o diretor ou o secretário vigente do clube (PERM-AUD-003, CA-AUD-009).
                CREATE POLICY admin_access_requests_update ON admin_access_requests FOR UPDATE
                    USING (club_id = app_club_id() AND EXISTS (SELECT 1 FROM memberships m JOIN role_assignments r ON r.membership_id = m.id
                        WHERE m.person_id = app_person_id() AND m.club_id = app_club_id() AND m.status = 'Active'
                          AND r.ended_at IS NULL AND r.role IN ('Director', 'Secretary')))
                    WITH CHECK (club_id = app_club_id());

                ALTER TABLE audit_entries ENABLE ROW LEVEL SECURITY;
                CREATE POLICY audit_entries_select ON audit_entries FOR SELECT
                    USING (club_id = app_club_id() OR (scope = 'Platform' AND app_is_platform_admin()));
                -- Sem contexto, nada; sem clube, só com pessoa ou admin; o ator é sempre quem age (CA-TEN-001, CA-AUD-010).
                CREATE POLICY audit_entries_insert ON audit_entries FOR INSERT
                    WITH CHECK ((club_id = app_club_id() OR (club_id IS NULL AND (app_person_id() IS NOT NULL OR app_is_platform_admin())))
                        AND (actor_person_id IS NULL OR actor_person_id = app_person_id()));

                ALTER TABLE notifications ENABLE ROW LEVEL SECURITY;
                CREATE POLICY notifications_select ON notifications FOR SELECT USING (recipient_person_id = app_person_id());
                -- Aviso de clube só dentro do clube; sem clube, só o admin (RN-NTF-001, CA-TEN-001).
                CREATE POLICY notifications_insert ON notifications FOR INSERT
                    WITH CHECK (club_id = app_club_id() OR (club_id IS NULL AND app_is_platform_admin()));
                CREATE POLICY notifications_update ON notifications FOR UPDATE
                    USING (recipient_person_id = app_person_id()) WITH CHECK (recipient_person_id = app_person_id());

                -- Busca e convite (RN-JOIN-001): só dados do clube, sem membros, e só os ativos.
                CREATE VIEW club_directory WITH (security_barrier) AS
                    SELECT id, name, city, church FROM clubs WHERE status = 'Active';
                -- Admin ao nomear diretor (RN-ORG-009, D-148): dados não pessoais do clube, só com o papel de admin desta requisição.
                CREATE VIEW admin_club_overview WITH (security_barrier) AS
                    SELECT c.id, c.name, c.city,
                           EXISTS (SELECT 1 FROM role_assignments r
                                   WHERE r.club_id = c.id AND r.role = 'Director' AND r.ended_at IS NULL) AS has_director
                    FROM clubs c WHERE c.status = 'Active' AND app_is_platform_admin();

                -- Convite vigente de um clube ativo (D-162), sem listar códigos.
                CREATE FUNCTION club_id_by_invite(p_code text) RETURNS uuid
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$ SELECT id FROM clubs WHERE invite_code = p_code AND status = 'Active' $$;
                -- Estado do solicitante de uma fundação pendente, só para o admin (RN-TEN-003, RN-TEN-004, D-137, RN-AUD-006): se é
                -- diretor e cada participação ativa ou suspensa (uma linha por participação; sem nenhuma, uma linha com nulos).
                CREATE FUNCTION founding_requester_state(p_request uuid)
                    RETURNS TABLE (director_club_id uuid, membership_id uuid, membership_club_id uuid)
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT (SELECT r.club_id FROM role_assignments r JOIN memberships d ON d.id = r.membership_id
                                WHERE d.person_id = f.requester_person_id AND r.role = 'Director' AND r.ended_at IS NULL LIMIT 1),
                               m.id, m.club_id
                        FROM founding_requests f
                        LEFT JOIN memberships m ON m.person_id = f.requester_person_id AND m.status IN ('Active', 'Suspended')
                        WHERE f.id = p_request AND f.status = 'Pending' AND app_is_platform_admin()
                    $$;
                -- Clubes com prazos de pedido de entrada a tratar (RN-JOIN-005, RN-JOIN-013, D-141, D-165): só IDs, e só para a
                -- tarefa agendada, que roda sem pessoa nem clube no contexto.
                CREATE FUNCTION join_request_clubs_due(p_now timestamptz, p_remind_before interval, p_purge_after interval)
                    RETURNS SETOF uuid
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT DISTINCT club_id FROM join_requests
                        WHERE app_person_id() IS NULL AND app_club_id() IS NULL
                          AND ((status = 'Queued' AND (expires_at <= p_now
                                  OR (reminder_sent_at IS NULL AND expires_at - p_remind_before <= p_now)))
                               OR (status IN ('Rejected', 'Expired', 'Cancelled') AND closed_at <= p_now - p_purge_after))
                    $$;
                -- Conta pelo sub do Google, só para o login, que roda sem pessoa nem clube no contexto (RN-ID-001).
                CREATE FUNCTION account_by_google_subject(p_subject text) RETURNS TABLE (account_id uuid, email text)
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT id, email FROM accounts
                        WHERE google_subject = p_subject AND app_person_id() IS NULL AND app_club_id() IS NULL
                    $$;
                -- E-mail só para a própria pessoa, ou para o admin dentro do clube do membro (RN-PRV-001, PERM-AUD-002).
                CREATE FUNCTION account_email(p_person uuid) RETURNS text
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT a.email FROM accounts a
                        WHERE a.person_id = p_person
                          AND (p_person = app_person_id()
                               OR (app_is_platform_admin() AND EXISTS (SELECT 1 FROM memberships m
                                   WHERE m.person_id = p_person AND m.club_id = app_club_id())))
                    $$;
                -- Endereço de uma mensagem da fila ainda não enviada, só para a tarefa agendada (RN-NTF-001).
                CREATE FUNCTION outbox_recipient_email(p_message uuid) RETURNS text
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT a.email FROM email_outbox o JOIN accounts a ON a.id = o.recipient_account_id
                        WHERE o.id = p_message AND o.sent_at IS NULL AND app_person_id() IS NULL AND app_club_id() IS NULL
                    $$;
                -- Admins adultos, para os avisos (D-146): só IDs; o corte de idade vem da aplicação.
                CREATE FUNCTION adult_platform_admin_ids(p_born_on_or_before date) RETURNS SETOF uuid
                    LANGUAGE sql STABLE SECURITY DEFINER SET search_path = pg_catalog, public, pg_temp
                    AS $$
                        SELECT a.person_id FROM accounts a JOIN persons p ON p.id = a.person_id
                        WHERE a.is_platform_admin AND p.birth_date <= p_born_on_or_before
                    $$;
                REVOKE ALL ON FUNCTION club_id_by_invite(text), founding_requester_state(uuid),
                    join_request_clubs_due(timestamptz, interval, interval), account_by_google_subject(text), account_email(uuid),
                    outbox_recipient_email(uuid), adult_platform_admin_ids(date) FROM PUBLIC;

                GRANT INSERT, UPDATE ON clubs, persons TO dbv_app;
                -- Na conta, o app só insere e troca o e-mail: o sub e o papel de admin não mudam por ele (RN-ID-001, RN-AUD-005).
                GRANT INSERT ON accounts TO dbv_app;
                GRANT UPDATE (email) ON accounts TO dbv_app;
                GRANT SELECT, INSERT, UPDATE ON units, role_assignments, director_nominations, founding_requests,
                    notifications, email_outbox TO dbv_app;
                -- No pedido de acesso do admin, o app só atualiza a autorização, a negação e a revogação: clube, solicitante,
                -- motivo, alvos, campos e prazo ficam fora do alcance dele (PERM-AUD-003, CA-AUD-009, D-121).
                GRANT SELECT, INSERT ON admin_access_requests TO dbv_app;
                GRANT UPDATE (status, decided_by_person_id, decided_at, valid_until, revoked_by_person_id, revoked_at)
                    ON admin_access_requests TO dbv_app;
                -- No consentimento, o app só atualiza o clube aprovado na fundação e o encerramento ou a revogação: titular, tipo,
                -- contexto e versão do termo ficam fora do alcance dele (CA-PRV-008, D-121).
                GRANT SELECT, INSERT ON consents TO dbv_app;
                GRANT UPDATE (club_id, revoked_at, ended_at, end_cause) ON consents TO dbv_app;
                GRANT SELECT, INSERT ON founding_records, audit_entries TO dbv_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON join_requests TO dbv_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON sessions TO dbv_app;
                GRANT SELECT ON club_directory, admin_club_overview TO dbv_app;
                GRANT EXECUTE ON FUNCTION club_id_by_invite(text), founding_requester_state(uuid),
                    join_request_clubs_due(timestamptz, interval, interval), account_by_google_subject(text), account_email(uuid),
                    outbox_recipient_email(uuid), adult_platform_admin_ids(date) TO dbv_app;

                -- E-mail e sub da conta fechados ao app: só pelas funções acima (RN-PRV-001).
                REVOKE SELECT ON accounts FROM dbv_app;
                GRANT SELECT (id, person_id, is_platform_admin, created_at) ON accounts TO dbv_app;
                -- Participação não se apaga: encerra (RN-JOIN-015, D-121).
                DROP POLICY memberships_delete ON memberships;
                REVOKE DELETE ON memberships FROM dbv_app;

                ALTER TABLE accounts ALTER COLUMN google_subject DROP DEFAULT;
                ALTER TABLE clubs ALTER COLUMN invite_code DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE POLICY memberships_delete ON memberships FOR DELETE
                    USING (club_id = NULLIF(current_setting('app.club_id', true), '')::uuid);
                GRANT DELETE ON memberships TO dbv_app;
                REVOKE SELECT (id, person_id, is_platform_admin, created_at) ON accounts FROM dbv_app;
                GRANT SELECT ON accounts TO dbv_app;
                ALTER TABLE join_requests DROP CONSTRAINT IF EXISTS fk_join_requests_consent_same_club_person;
                DROP INDEX IF EXISTS ux_consents_id_club_person;
                DROP FUNCTION IF EXISTS adult_platform_admin_ids(date);
                DROP FUNCTION IF EXISTS outbox_recipient_email(uuid);
                DROP FUNCTION IF EXISTS account_email(uuid);
                DROP FUNCTION IF EXISTS account_by_google_subject(text);
                DROP FUNCTION IF EXISTS join_request_clubs_due(timestamptz, interval, interval);
                DROP FUNCTION IF EXISTS founding_requester_state(uuid);
                DROP FUNCTION IF EXISTS club_id_by_invite(text);
                DROP VIEW IF EXISTS admin_club_overview;
                DROP VIEW IF EXISTS club_directory;
                DROP POLICY IF EXISTS persons_update ON persons;
                DROP POLICY IF EXISTS persons_insert ON persons;
                DROP POLICY IF EXISTS persons_select ON persons;
                CREATE POLICY persons_select ON persons FOR SELECT
                    USING (id = NULLIF(current_setting('app.person_id', true), '')::uuid
                        OR EXISTS (SELECT 1 FROM memberships m
                                   WHERE m.person_id = persons.id
                                     AND m.club_id = NULLIF(current_setting('app.club_id', true), '')::uuid));
                DROP POLICY IF EXISTS clubs_update ON clubs;
                DROP POLICY IF EXISTS clubs_insert ON clubs;
                DROP POLICY IF EXISTS clubs_select ON clubs;
                CREATE POLICY clubs_select ON clubs FOR SELECT
                    USING (id = NULLIF(current_setting('app.club_id', true), '')::uuid
                        OR (NULLIF(current_setting('app.club_id', true), '') IS NULL
                            AND EXISTS (SELECT 1 FROM memberships m
                                        WHERE m.club_id = clubs.id
                                          AND m.person_id = NULLIF(current_setting('app.person_id', true), '')::uuid
                                          AND m.ended_at IS NULL)));
                REVOKE INSERT, UPDATE ON clubs, persons, accounts FROM dbv_app;
                """);

            migrationBuilder.DropTable(
                name: "admin_access_requests");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "director_nominations");

            migrationBuilder.DropTable(
                name: "email_outbox");

            migrationBuilder.DropTable(
                name: "founding_records");

            migrationBuilder.DropTable(
                name: "join_requests");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "role_assignments");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "consents");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "founding_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_persons_sex",
                table: "persons");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_memberships_club_id_id",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ux_memberships_one_active_per_person",
                table: "memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_ended",
                table: "memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_suspended",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "ux_clubs_invite_code",
                table: "clubs");

            migrationBuilder.DropIndex(
                name: "ux_accounts_google_subject",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "sex",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "status",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "status_cause",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "status_reason",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "church",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "city",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "founding_request_id",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "invite_code",
                table: "clubs");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "email",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "google_subject",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "is_platform_admin",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "persons",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_is_platform_admin(); DROP FUNCTION IF EXISTS app_person_id(); DROP FUNCTION IF EXISTS app_club_id();");
        }
    }
}
