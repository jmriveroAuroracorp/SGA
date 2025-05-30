using Microsoft.EntityFrameworkCore;

namespace SGA_Api.Data
{
    public class MobilityWH3DbContext : DbContext
    {
        public MobilityWH3DbContext(DbContextOptions<MobilityWH3DbContext> options)
            : base(options)
        {
        }

    }
}
