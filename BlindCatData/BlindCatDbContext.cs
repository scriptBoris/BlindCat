using BlindCatData.Models;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace BlindCatData;

public class BlindCatDbContext : DbContext
{
    private readonly string _dataPath;
    private readonly bool _isDesignTime;

    public DbSet<MetaDb> Meta => Set<MetaDb>();
    public DbSet<ContentStorageDb> Contents => Set<ContentStorageDb>();
    public DbSet<AlbumStorageDb> Albums => Set<AlbumStorageDb>();

    private BlindCatDbContext(string _dataPath, bool _isDesignTime)
    {
        this._dataPath = _dataPath;
        this._isDesignTime = _isDesignTime;
    }

    public BlindCatDbContext(string dataPath) : this(dataPath, false)
    {
        Database.Migrate();
    }

    internal static BlindCatDbContext DesignInstance()
    {
        var instance = new BlindCatDbContext(null!, true);
        instance.Database.EnsureDeleted();
        instance.Database.Migrate();
        return instance;
    }

    public static BlindCatDbContext JustConnect(string dataPath)
    {
        var db = new BlindCatDbContext(dataPath, false);
        return db;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_isDesignTime)
        {
            optionsBuilder.UseSqlite($@"Data Source=migrationMake.db");
        }
        else
        {
            if (_dataPath == null)
                throw new InvalidOperationException("Не указан обязательный путь к БД");

            optionsBuilder.UseSqlite($@"Data Source={_dataPath}");
        }
    }
}

/// <summary>
/// Для расспознования при CLI команде фиксаций миграций
/// </summary>
public class DataBaseCLIFactory : IDesignTimeDbContextFactory<BlindCatDbContext>
{
    public BlindCatDbContext CreateDbContext(string[] args)
    {
        return BlindCatDbContext.DesignInstance();
    }
}