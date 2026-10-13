using System;
using ClaimDesk.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ClaimDesk.Api.Data.Migrations
{
    [DbContext(typeof(ClaimDeskDbContext))]
    [Migration("20261013114336_AuditTriggerAndReport")]
    partial class AuditTriggerAndReport
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("ClaimDesk.Api.Domain.Claim", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int?>("ApprovedByUserId")
                        .HasColumnType("int");

                    b.Property<string>("ClaimNumber")
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)")
                        .HasComputedColumnSql("'CLM-' + RIGHT('000000' + CAST([Id] AS varchar(10)), 6)", true);

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<int>("FiledByUserId")
                        .HasColumnType("int");

                    b.Property<DateOnly>("IncidentDate")
                        .HasColumnType("date");

                    b.Property<int>("LastChangedByUserId")
                        .HasColumnType("int");

                    b.Property<int>("PolicyId")
                        .HasColumnType("int");

                    b.Property<string>("RejectionReason")
                        .HasMaxLength(300)
                        .HasColumnType("nvarchar(300)");

                    b.Property<int?>("ReviewedByUserId")
                        .HasColumnType("int");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.HasIndex("ApprovedByUserId");

                    b.HasIndex("ClaimNumber")
                        .IsUnique();

                    b.HasIndex("FiledByUserId");

                    b.HasIndex("LastChangedByUserId");

                    b.HasIndex("PolicyId");

                    b.HasIndex("ReviewedByUserId");

                    b.ToTable("Claims", t =>
                        {
                            t.HasTrigger("trg_Claims_StatusAudit");
                        });

                    b.HasAnnotation("SqlServer:UseSqlOutputClause", false);
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.ClaimAudit", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("ChangedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("ChangedByUserId")
                        .HasColumnType("int");

                    b.Property<int>("ClaimId")
                        .HasColumnType("int");

                    b.Property<string>("NewStatus")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("OldStatus")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.HasIndex("ChangedByUserId");

                    b.HasIndex("ClaimId");

                    b.ToTable("ClaimAudits");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("Customers");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Policy", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("CustomerId")
                        .HasColumnType("int");

                    b.Property<DateOnly>("EndDate")
                        .HasColumnType("date");

                    b.Property<string>("PolicyNumber")
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)")
                        .HasComputedColumnSql("'POL-' + RIGHT('000000' + CAST([Id] AS varchar(10)), 6)", true);

                    b.Property<decimal>("Premium")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateOnly>("StartDate")
                        .HasColumnType("date");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<decimal>("SumInsured")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.HasIndex("CustomerId");

                    b.HasIndex("PolicyNumber")
                        .IsUnique();

                    b.ToTable("Policies");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.User", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.HasKey("Id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Claim", b =>
                {
                    b.HasOne("ClaimDesk.Api.Domain.User", "ApprovedBy")
                        .WithMany()
                        .HasForeignKey("ApprovedByUserId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("ClaimDesk.Api.Domain.User", "FiledBy")
                        .WithMany()
                        .HasForeignKey("FiledByUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ClaimDesk.Api.Domain.User", null)
                        .WithMany()
                        .HasForeignKey("LastChangedByUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ClaimDesk.Api.Domain.Policy", "Policy")
                        .WithMany("Claims")
                        .HasForeignKey("PolicyId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ClaimDesk.Api.Domain.User", "ReviewedBy")
                        .WithMany()
                        .HasForeignKey("ReviewedByUserId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("ApprovedBy");

                    b.Navigation("FiledBy");

                    b.Navigation("Policy");

                    b.Navigation("ReviewedBy");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.ClaimAudit", b =>
                {
                    b.HasOne("ClaimDesk.Api.Domain.User", "ChangedBy")
                        .WithMany()
                        .HasForeignKey("ChangedByUserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ClaimDesk.Api.Domain.Claim", "Claim")
                        .WithMany("Audits")
                        .HasForeignKey("ClaimId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ChangedBy");

                    b.Navigation("Claim");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Policy", b =>
                {
                    b.HasOne("ClaimDesk.Api.Domain.Customer", "Customer")
                        .WithMany("Policies")
                        .HasForeignKey("CustomerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Claim", b =>
                {
                    b.Navigation("Audits");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Customer", b =>
                {
                    b.Navigation("Policies");
                });

            modelBuilder.Entity("ClaimDesk.Api.Domain.Policy", b =>
                {
                    b.Navigation("Claims");
                });
#pragma warning restore 612, 618
        }
    }
}
