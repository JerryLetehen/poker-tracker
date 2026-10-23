using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using poker_web_api.Entities;

namespace poker_web_api.Data;

public class PokerDbContext : DbContext
{
    public PokerDbContext(DbContextOptions<PokerDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameEntry> GameEntries => Set<GameEntry>();
    public DbSet<AccessUser> AccessUsers => Set<AccessUser>();
    public DbSet<PendingAccessRequest> PendingAccessRequests => Set<PendingAccessRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntry>()
            .Property(e => e.Type)
            .HasConversion(new ValueConverter<EntryType, string>(
                v => v == EntryType.BuyIn ? "buy-in" : "cash-out",
                v => v == "buy-in" ? EntryType.BuyIn : EntryType.CashOut
            ));

        modelBuilder.Entity<AccessUser>(entity =>
        {
            entity.HasIndex(u => u.TelegramUserId).IsUnique();
            entity.Property(u => u.Scope)
                .HasConversion(new ValueConverter<AccessScope, string>(
                    v => v == AccessScope.Admin ? "admin" : "public",
                    v => v == "admin" ? AccessScope.Admin : AccessScope.Public
                ));
        });

        modelBuilder.Entity<PendingAccessRequest>()
            .HasIndex(r => r.TelegramUserId)
            .IsUnique();
    }
}
