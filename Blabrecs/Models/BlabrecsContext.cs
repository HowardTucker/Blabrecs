using Microsoft.AspNet.Identity.EntityFramework;
using System.Data.Entity;

namespace Blabrecs.Models
{
    public class BlabrecsContext : IdentityDbContext<User>
    {
        public BlabrecsContext()
            : base("BlabrecsContext")
        {
        }

        public DbSet<Game> Games { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<Player> Players { get; set; }

        public DbSet<Letter> Letters { get; set; }

        public DbSet<Dictionary> Dictionary { get; set; }
    }
}