using BlindCatData.Models;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace BlindCatData;

public class MauiDbContext : DbContext
{
    private readonly string _dataPath;
    private readonly bool _isDesignTime;

    public DbSet<MetaDb> Meta => Set<MetaDb>();
    public DbSet<ContentStorageDb> Contents => Set<ContentStorageDb>();

    private MauiDbContext(string dataPath, bool isDesignTime)
    {
        _dataPath = dataPath;
    }

    public MauiDbContext(string dataPath)
    {
        _dataPath = dataPath;
        Database.Migrate();
    }

    public MauiDbContext(bool isDesignTime)
    {
        _dataPath = null!;
        _isDesignTime = isDesignTime;

        if (isDesignTime)
            Database.EnsureDeleted();

        Database.Migrate();
    }

    public static MauiDbContext JustConnect(string dataPath)
    {
        var db = new MauiDbContext(dataPath, false);
        return db;
    }


    public override void Dispose()
    {
        base.Dispose();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!_isDesignTime)
        {
            if (_dataPath == null)
                throw new InvalidOperationException("Не указан обязательный путь к БД");

            optionsBuilder.UseSqlite($@"Data Source={_dataPath}");
        }
        else
        {
            optionsBuilder.UseSqlite($@"Data Source=migrationMake.db");
        }
    }
}

/// <summary>
/// Для расспознования при CLI команде фиксаций миграций
/// </summary>
public class DataBaseCLIFactory : IDesignTimeDbContextFactory<MauiDbContext>
{
    public MauiDbContext CreateDbContext(string[] args)
    {
        return new MauiDbContext(true);
    }
}