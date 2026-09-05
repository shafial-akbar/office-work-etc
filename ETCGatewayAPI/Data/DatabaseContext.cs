using Etc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ETCGatewayAPI.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        // Gateway Entities
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<DoTransaction> DoTransactions { get; set; }
        public DbSet<TransactionLog> TransactionLogs { get; set; }
        public DbSet<Settlement> Settlements { get; set; }
        public DbSet<ApiUser> ApiUsers { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<RequestLog> RequestLogs { get; set; }
        public DbSet<ApiToken> ApiTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Customer Configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CustomerId).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Status).HasDefaultValue("Active");
            });

            // 2. Wallet Configuration
            modelBuilder.Entity<Wallet>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.WalletNo).IsUnique();

                entity.Property(e => e.WalletNo)
                      .IsRequired()
                      .HasMaxLength(13)
                      .IsFixedLength(); // CHAR(13) in PostgreSQL

                entity.HasIndex(e => e.MobileNo).IsUnique();
                entity.Property(e => e.MobileNo).IsRequired().HasMaxLength(20);

                entity.Property(e => e.CompanyName)
                      .HasDefaultValue("SONALI BANK PLC")
                      .HasMaxLength(100);

                entity.Property(e => e.Type)
                      .HasDefaultValue("BANK")
                      .HasMaxLength(50);

                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Wallets)
                      .HasForeignKey(e => e.CustomerId);

                entity.Property(e => e.Balance).HasColumnType("decimal(18,2)").HasDefaultValue(0.00m);
                entity.Property(e => e.Currency).HasDefaultValue("BDT");
                entity.Property(e => e.Status).HasDefaultValue("Active");
            });

            // 3. DoTransaction Configuration
            modelBuilder.Entity<DoTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionAmount).HasColumnType("numeric(18,2)");
                entity.Property(e => e.SettlDate).IsRequired(false);

                // Indexes for Fast Search
                entity.HasIndex(e => e.WalletId);
                entity.HasIndex(e => e.BankTxnId).IsUnique();
                entity.HasIndex(e => e.PartnerTxnId);
                entity.HasIndex(e => e.RefNo1).IsUnique();

                entity.HasOne(e => e.Wallet)
                      .WithMany(w => w.DoTransactions)
                      .HasForeignKey(e => e.WalletId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 4. Vehicle Configuration
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.HasOne(v => v.Wallet)
                      .WithMany(w => w.Vehicles)
                      .HasForeignKey(v => v.WalletId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 5. Settlement Configuration
            modelBuilder.Entity<Settlement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BatchProcessId).IsUnique();
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasDefaultValue("Pending");
            });

            // 6. RequestLog & ApiToken
            modelBuilder.Entity<RequestLog>().HasKey(r => r.Id);
            modelBuilder.Entity<ApiToken>().HasKey(t => t.Id);

            // 7. Global Table Prefix (TBL_) Application
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var currentTableName = entity.GetTableName();
                if (!string.IsNullOrEmpty(currentTableName))
                {
                    entity.SetTableName($"TBL_{currentTableName}");
                }
            }
        }
    }
}