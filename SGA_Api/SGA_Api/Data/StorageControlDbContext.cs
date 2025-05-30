using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Data
{
    public class StorageControlDbContext : DbContext
    {
        public StorageControlDbContext(DbContextOptions<StorageControlDbContext> options)
            : base(options)
        {
        }
    }
}
