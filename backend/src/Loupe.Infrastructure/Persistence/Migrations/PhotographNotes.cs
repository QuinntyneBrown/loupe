using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;



namespace Loupe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LibraryDbContext))]
    [Migration("20260907182114_PhotographNotes")]
    public class PhotographNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "photographs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "photographs");
        }
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

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
