namespace Blabrecs.Migrations
{
    using Blabrecs.Models;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using System.Data.Entity.Migrations;

    internal sealed class Configuration : DbMigrationsConfiguration<Blabrecs.Models.BlabrecsContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "Blabrecs.Models.BlabrecsContext";
        }

        private bool AddUser(BlabrecsContext context)
        {
            IdentityResult identityResult;
            UserManager<User> userManager = new UserManager<User>(new UserStore<User>(context));
            var user = new User()
            {
                UserName = "player1"
            };
            var user2 = new User()
            {
                UserName = "player2"
            };
            if (userManager.FindByName(user.UserName) != null)
            {
                return true;
            }
            identityResult = userManager.Create(user, "password");
            identityResult = userManager.Create(user2, "password");
            return identityResult.Succeeded;
        }

        protected override void Seed(Blabrecs.Models.BlabrecsContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
            AddUser(context);
        }
    }
}