using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using Microsoft.EntityFrameworkCore.Migrations;



namespace Loupe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LibraryDbContext))]
    [Migration("20260907232549_BackgroundOperations")]
    public class BackgroundOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_operations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_background_operations_OwnerId_Type_ResourceId",
                table: "background_operations",
                columns: new[] { "OwnerId", "Type", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_background_operations_Status_CreatedAt",
                table: "background_operations",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_operations");
        }
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Loupe.Domain.Deletions.DeletionOperation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("DeletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset?>("LastAttemptAt")
                        .HasColumnType("timestamp with time zone");

                    b.PrimitiveCollection<string[]>("MediaKeys")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("ResourceType")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.HasKey("Id");

                    b.HasIndex("CompletedAt", "LastAttemptAt", "DeletedAt");

                    b.HasIndex("OwnerId", "ResourceType", "ResourceId")
                        .IsUnique();

                    b.ToTable("deletions", "journal");
                });

            modelBuilder.Entity("Loupe.Domain.Operations.BackgroundOperation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("InputJson")
                        .HasColumnType("jsonb");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Mode")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("Model")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("PromptVersion")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("Status", "CreatedAt");

                    b.HasIndex("OwnerId", "Type", "ResourceId");

                    b.ToTable("background_operations", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Operations.OperationReceipt", b =>
                {
                    b.Property<string>("OwnerId")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("OperationType")
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Key")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("PayloadHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.HasKey("OwnerId", "OperationType", "Key");

                    b.HasIndex("CreatedAt");

                    b.ToTable("operation_receipts", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Photographs.Photograph", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Height")
                        .HasColumnType("integer");

                    b.Property<string>("ImageKey")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<string>("OwnerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PreviewKey")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("Revision")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasDefaultValue(1L);

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Width")
                        .HasColumnType("integer");

                    b.ComplexProperty(typeof(Dictionary<string, object>), "Brief", "Loupe.Domain.Photographs.Photograph.Brief#CritiqueBrief", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<int?>("Experience");

                            b1.Property<string>("Genre");

                            b1.Property<string>("Intent");

                            b1.Property<string>("RequestedFeedback");

                            b1
                                .ToJson("Brief")
                                .HasColumnType("jsonb");
                        });

                    b.ComplexProperty(typeof(Dictionary<string, object>), "Exif", "Loupe.Domain.Photographs.Photograph.Exif#CaptureMetadata", b1 =>
                        {
                            b1.IsRequired();

                            b1.Property<string>("Aperture");

                            b1.Property<string>("Camera");

                            b1.Property<string>("CapturedAt");

                            b1.Property<string>("FocalLength");

                            b1.Property<string>("Iso");

                            b1.Property<string>("Lens");

                            b1.Property<string>("ShutterSpeed");

                            b1
                                .ToJson("Exif")
                                .HasColumnType("jsonb");
                        });

                    b.HasKey("Id");

                    b.HasIndex("OwnerId", "CreatedAt", "Id");

                    b.ToTable("photographs", (string?)null);
                });

            modelBuilder.Entity("Loupe.Domain.Sessions.ApplicationSession", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Issuer")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("LastSeenAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Subject")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("sessions", (string?)null);
                });
        }
    }
}
