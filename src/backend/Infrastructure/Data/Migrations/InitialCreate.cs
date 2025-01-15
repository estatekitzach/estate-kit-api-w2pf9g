using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EstateKit.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Initial database migration that creates the foundational schema for the EstateKit system
    /// with comprehensive security features, audit logging, and performance optimizations.
    /// </summary>
    public class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create Users table with security features
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    DateOfBirth = table.Column<string>(maxLength: 256, nullable: false),
                    BirthPlace = table.Column<string>(maxLength: 500, nullable: true),
                    MaritalStatus = table.Column<string>(maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    LastModifiedBy = table.Column<string>(maxLength: 100, nullable: false),
                    SecurityStamp = table.Column<string>(maxLength: 100, nullable: false),
                    LastAuditDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                });

            // Create Contacts table with encryption
            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    FirstName = table.Column<string>(maxLength: 100, nullable: false),
                    LastName = table.Column<string>(maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(maxLength: 100, nullable: true),
                    MaidenName = table.Column<string>(maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_Contacts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Documents table with S3 integration
            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    FrontImageUrl = table.Column<string>(maxLength: 1000, nullable: true),
                    BackImageUrl = table.Column<string>(maxLength: 1000, nullable: true),
                    Location = table.Column<string>(maxLength: 500, nullable: true),
                    InKit = table.Column<bool>(nullable: false, defaultValue: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    IsProcessed = table.Column<bool>(nullable: false, defaultValue: false),
                    S3BucketName = table.Column<string>(maxLength: 100, nullable: false),
                    S3KeyPrefix = table.Column<string>(maxLength: 200, nullable: false),
                    SecurityClassification = table.Column<string>(maxLength: 50, nullable: false),
                    ProcessingStatus = table.Column<string>(maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_Documents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Assets table
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 200, nullable: false),
                    Description = table.Column<string>(maxLength: 2000, nullable: true),
                    Type = table.Column<int>(nullable: false),
                    Location = table.Column<string>(maxLength: 500, nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccessInformation = table.Column<string>(maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_Assets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Identifiers table with critical security
            migrationBuilder.CreateTable(
                name: "Identifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    Value = table.Column<string>(maxLength: 256, nullable: false),
                    IssuingAuthority = table.Column<string>(maxLength: 100, nullable: false),
                    IssueDate = table.Column<DateTime>(nullable: false),
                    ExpiryDate = table.Column<DateTime>(nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true),
                    AuditTrail = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identifiers", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_Identifiers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create ContactMethods table
            migrationBuilder.CreateTable(
                name: "ContactMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ContactId = table.Column<Guid>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    Value = table.Column<string>(maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactMethods", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_ContactMethods_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Relationships table
            migrationBuilder.CreateTable(
                name: "Relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ContactId = table.Column<Guid>(nullable: false),
                    RelatedContactId = table.Column<Guid>(nullable: false),
                    Type = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relationships", x => x.Id)
                        .Annotation("Npgsql:IndexMethod", "btree");
                    table.ForeignKey(
                        name: "FK_Relationships_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Relationships_Contacts_RelatedContactId",
                        column: x => x.RelatedContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create indexes for performance optimization
            migrationBuilder.CreateIndex(
                name: "IX_Users_LastName_FirstName",
                table: "Contacts",
                columns: new[] { "LastName", "FirstName" })
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId_Type",
                table: "Documents",
                columns: new[] { "UserId", "Type" })
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_UserId_Type",
                table: "Assets",
                columns: new[] { "UserId", "Type" })
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.CreateIndex(
                name: "IX_Identifiers_UserId_Type",
                table: "Identifiers",
                columns: new[] { "UserId", "Type" },
                unique: true)
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.CreateIndex(
                name: "IX_ContactMethods_ContactId_Type",
                table: "ContactMethods",
                columns: new[] { "ContactId", "Type" })
                .Annotation("Npgsql:IndexMethod", "btree");

            migrationBuilder.CreateIndex(
                name: "IX_Relationships_ContactId_RelatedContactId",
                table: "Relationships",
                columns: new[] { "ContactId", "RelatedContactId" },
                unique: true)
                .Annotation("Npgsql:IndexMethod", "btree");

            // Create audit logging triggers
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_trigger_func()
                RETURNS TRIGGER AS $$
                BEGIN
                    INSERT INTO audit_logs (
                        table_name,
                        record_id,
                        action,
                        old_values,
                        new_values,
                        changed_by,
                        changed_at
                    )
                    VALUES (
                        TG_TABLE_NAME,
                        NEW.id,
                        TG_OP,
                        CASE WHEN TG_OP = 'DELETE' THEN row_to_json(OLD) ELSE NULL END,
                        CASE WHEN TG_OP != 'DELETE' THEN row_to_json(NEW) ELSE NULL END,
                        current_user,
                        current_timestamp
                    );
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Apply audit triggers to all tables
            string[] tables = { "Users", "Contacts", "Documents", "Assets", "Identifiers", "ContactMethods", "Relationships" };
            foreach (var table in tables)
            {
                migrationBuilder.Sql($@"
                    CREATE TRIGGER audit_{table.ToLower()}_trigger
                    AFTER INSERT OR UPDATE OR DELETE ON {table}
                    FOR EACH ROW EXECUTE FUNCTION audit_trigger_func();
                ");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove audit triggers
            string[] tables = { "Users", "Contacts", "Documents", "Assets", "Identifiers", "ContactMethods", "Relationships" };
            foreach (var table in tables)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS audit_{table.ToLower()}_trigger ON {table};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_trigger_func();");

            // Drop tables in reverse order of creation
            migrationBuilder.DropTable(name: "Relationships");
            migrationBuilder.DropTable(name: "ContactMethods");
            migrationBuilder.DropTable(name: "Identifiers");
            migrationBuilder.DropTable(name: "Assets");
            migrationBuilder.DropTable(name: "Documents");
            migrationBuilder.DropTable(name: "Contacts");
            migrationBuilder.DropTable(name: "Users");
        }
    }
}