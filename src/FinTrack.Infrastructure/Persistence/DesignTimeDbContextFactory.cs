using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinTrack.Infrastructure.Persistence;

// Usada apenas por `dotnet ef`; a conexão não é aberta ao gerar migrations.
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinTrackDbContext>
{
    public FinTrackDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinTrackDbContext>()
            .UseSqlServer("Server=localhost;Database=FinTrackDesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new FinTrackDbContext(options);
    }
}
