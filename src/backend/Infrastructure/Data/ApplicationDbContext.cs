using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EstateKit.Core.Entities;
using EstateKit.Encryption;
using EstateKit.Auditing;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace EstateKit.Infrastructure.Data
{
    /// <summary>
    /// Database context class that manages entity configurations, relationships, and database operations
    /// with enhanced security features including field-level encryption and comprehensive audit logging.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        private readonly IConfiguration _configuration;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditService _auditService;

        // Entity Sets
        public DbSet<User> Users { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Identifier> Identifiers { get; set; }

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IConfiguration configuration,
            IEncryptionService encryptionService,
            IAuditService auditService) 
            : base(options)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
                throw new ArgumentNullException(nameof(modelBuilder));

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Contact.Id).IsUnique();
                entity.Property(e => e.DateOfBirth).IsEncrypted();
                entity.Property(e => e.BirthPlace).IsEncrypted();
                entity.HasQueryFilter(u => u.IsActive);
            });

            // Configure Contact entity
            modelBuilder.Entity<Contact>(entity =>
            {
                entity.ToTable("Contacts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsEncrypted().IsRequired();
                entity.Property(e => e.LastName).IsEncrypted().IsRequired();
                entity.Property(e => e.MiddleName).IsEncrypted();
                entity.Property(e => e.MaidenName).IsEncrypted();
                entity.HasIndex(e => e.LastName);
            });

            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("Documents");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.Type });
                entity.Property(e => e.FrontImageUrl).IsEncrypted();
                entity.Property(e => e.BackImageUrl).IsEncrypted();
                entity.Property(e => e.Location).IsEncrypted();
                entity.Property(e => e.Metadata).IsEncrypted();
            });

            // Configure Asset entity
            modelBuilder.Entity<Asset>(entity =>
            {
                entity.ToTable("Assets");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.Location).IsEncrypted();
                entity.Property(e => e.AccessInformation).IsEncrypted();
                entity.HasQueryFilter(a => a.IsActive);
            });

            // Configure Identifier entity
            modelBuilder.Entity<Identifier>(entity =>
            {
                entity.ToTable("Identifiers");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.Type });
                entity.Property(e => e.Value).IsEncrypted();
                entity.HasQueryFilter(i => i.IsActive);
            });

            // Configure relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.Contact)
                .WithOne()
                .HasForeignKey<Contact>("UserId")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Documents)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Identifiers)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Assets)
                .WithOne()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Restrict);

            // Configure database collation
            modelBuilder.UseCollation("en_US.utf8");

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder
                    .UseNpgsql(_configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null);
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    })
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                if (_configuration.GetValue<bool>("EnableDetailedErrors"))
                {
                    optionsBuilder.EnableDetailedErrors();
                    optionsBuilder.EnableSensitiveDataLogging();
                }
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var entries = ChangeTracker.Entries();
                foreach (var entry in entries)
                {
                    // Handle encryption for modified properties
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        await _encryptionService.EncryptEntityAsync(entry.Entity, cancellationToken);
                    }

                    // Create audit trail
                    await _auditService.CreateAuditTrailAsync(entry, cancellationToken);
                }

                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await _auditService.LogFailedOperationAsync(ex, cancellationToken);
                throw;
            }
        }

        public override int SaveChanges()
        {
            return SaveChangesAsync().GetAwaiter().GetResult();
        }
    }
}