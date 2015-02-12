using Blabrecs.Models;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Blabrecs.Hubs
{
    [HubName("game")]
    public class GameHub : Hub
    {
        private BlabrecsContext db = new BlabrecsContext();

        [Authorize]
        public void abandonGame(int GameId, bool gameOwner)
        {
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
            var game = db.Games.Include("Players").Include("Players.User").Include("Letters.Player").Include("Letters.Player.User").Where(g => g.Id == GameId).First();
            // if the game owner leaves or another player leaves mid game close the game for everyone
            if (gameOwner || game.Open == false)
            {
                Clients.Group(GameId.ToString()).onCloseGame();
                db.Games.Remove(game);
                db.SaveChanges();
            }
            else
            {
                // if a player leaves before the game has started then continue without that player
                game.Players.Remove(game.Players.Where(p => p.User == user).First());
                if (game.Players.Count == 0)
                {
                    db.Messages.RemoveRange(db.Messages.Where(m => m.Game.Id == game.Id));
                    db.Games.Remove(game);
                }
                db.SaveChanges();
                Clients.Caller.onAbandonGame();
                Groups.Remove(Context.ConnectionId, game.Id.ToString());
                Clients.Group(GameId.ToString()).onPlayerLeaves(new { name = user.UserName, score = 0 });
            }
            var games = db.Games.Where(g => g.Open == true).Select(g => new { g.Id, g.Name, g.NumberOfPlayers, g.Open, g.Active, CurrentlyConnectedPlayers = g.Players.Count });
            Clients.All.onActiveGames(games);
        }

        [Authorize]
        public void getGameState(int GameId)
        {
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
            var game = db.Games.Include("Players").Include("Players.User").Include("Letters.Player").Include("Letters.Player.User").Where(g => g.Id == GameId).First();
            var rackLetters = game.Letters.Where(l => l.Player != null && l.Player.User.Id == user.Id).Select(l => new { id = l.Id, letterType = l.Type, row = l.Row, column = l.Column, rackPosition = l.RackPosition });
            var letters = game.Letters.Where(l => l.Row != null).Select(l => new { letterType = l.Type, row = l.Row, column = l.Column });
            var isTurn = game.Players.Where(p => p.User.Id == user.Id).First().IsTurn;
            var gameOver = game.Players.Where(p => p.Letters.Count() == 0).Any();
            Clients.Caller.onUpdateGameState(rackLetters, letters, isTurn, game.Players.Select(p => new { name = p.User.UserName, score = p.Score, isTurn = p.IsTurn }), gameOver);
        }

        [Authorize]
        public void joinGame(int GameId)
        {
            var game = db.Games.Include("Players").Where(g => g.Id == GameId).First();
            if (game != null)
            {
                if (game.Players.Count < game.NumberOfPlayers)
                {
                    User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
                    if (!game.Players.Where(p => p.User == user).Any())
                    {
                        game.Players.Add(new Player() { PlayerNumber = game.Players.Count + 1, User = user, Score = 0 });

                        db.SaveChanges();

                        var games = db.Games.Where(g => g.Open == true).Select(g => new { g.Id, g.Name, g.NumberOfPlayers, g.Open, g.Active, CurrentlyConnectedPlayers = g.Players.Count });
                        Clients.All.onActiveGames(games);
                        Clients.Group(GameId.ToString()).onPlayerJoins(new { name = user.UserName, score = 0 });
                        var players = game.Players.Select(p => new { user = p.User.UserName, score = 0 }).ToList();
                        var letters = new List<Letter>();
                        Clients.Caller.onJoinGame(game.Id, game.Players.Select(p => new { name = p.User.UserName, score = 0 }), letters.Select(l => new { letterType = l.Type, row = l.Row, column = l.Column }));
                        Groups.Add(Context.ConnectionId, game.Id.ToString());
                    }
                    else
                    {
                        Clients.Caller.onAlreadyInGame();
                    }
                }
            }
        }

        [Authorize]
        public void NewGame(string Name, int NumberOfPlayers)
        {
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();

            Game game = new Game() { Name = Name, NumberOfPlayers = NumberOfPlayers, Open = true, Active = true, Players = new List<Player>() };

            game.Players.Add(new Player() { PlayerNumber = 1, User = user, Score = 0 });
            db.Games.Add(game);
            db.SaveChanges();

            var games = db.Games.Where(g => g.Open == true).Select(g => new { Id = g.Id, g.Name, g.NumberOfPlayers, g.Open, g.Active, CurrentlyConnectedPlayers = g.Players.Count });
            Clients.All.onActiveGames(games);
            Groups.Add(Context.ConnectionId, game.Id.ToString());
            Clients.Caller.onSwitchToGame(new { id = game.Id, name = game.Name }, new { name = user.UserName, score = 0 });
        }

        /// <summary>
        /// Send a list of open games to a player when they connect
        /// </summary>
        /// <returns></returns>
        public override System.Threading.Tasks.Task OnConnected()
        {
            var games = db.Games.Where(g => g.Open == true).Select(g => new { g.Id, g.Name, g.NumberOfPlayers, g.Open, g.Active, CurrentlyConnectedPlayers = g.Players.Count });
            Clients.Caller.onActiveGames(games);
            return base.OnConnected();
        }

        /// <summary>
        /// When a player disconnects remove them from any currently connected games
        /// </summary>
        /// <param name="stopCalled"></param>
        /// <returns></returns>
        public override System.Threading.Tasks.Task OnDisconnected(bool stopCalled)
        {
            bool gameRemoved = false;
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
            foreach (var game in db.Games.Include("Players").Where(g => g.Open == true && g.Players.Any(p => p.User.Id == user.Id)).ToList())
            {
                if (game.Players != null)
                {
                    db.Letters.RemoveRange(game.Letters.Where(l => l.Game.Id == game.Id));
                    game.Players.Remove(game.Players.Where(p => p.User == user).First());
                    Clients.Group(game.Id.ToString()).onPlayerLeaves(new { name = user.UserName, score = 0 });
                    if (game.Players.Count == 0)
                    {
                        db.Messages.RemoveRange(db.Messages.Where(m => m.Game.Id == game.Id));
                        db.Games.Remove(game);
                        gameRemoved = true;
                    }
                }
            }
            foreach (var game in db.Games.Include("Players").Where(g => g.Open == false && g.Players.Any(p => p.User.Id == user.Id)).ToList())
            {
                Clients.Group(game.Id.ToString()).onCloseGame();
            }
            db.SaveChanges();
            if (gameRemoved)
            {
                var games = db.Games.Where(g => g.Open == true).Select(g => new { g.Id, g.Name, g.NumberOfPlayers, g.Open, g.Active, CurrentlyConnectedPlayers = g.Players.Count });
                Clients.All.onActiveGames(games);
            }

            return base.OnDisconnected(stopCalled);
        }

        [Authorize]
        public void passTurn(int GameId, List<Letter> passedLetters)
        {
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
            var game = db.Games.Include("Players").Include("Players.User").Include("Letters.Player").Include("Letters.Player.User").Where(g => g.Id == GameId).First();
            var player = game.Players.Where(p => p.User.Id == user.Id).First();
            game.PassTurn(passedLetters, player, db);
            Clients.Group(GameId.ToString()).onGameStateUpdated();
        }

        [Authorize]
        public void SendMessage(int GameId, string message)
        {
            var game = db.Games.Include("Players").Where(g => g.Id == GameId).First();
            if (game != null)
            {
                User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
                if (game.Players.Where(p => p.User == user).Any())
                {
                    game.Messages.Add(new Message()
                    {
                        Game = game,
                        Contents = message,
                        User = user,
                        TimeSent = DateTime.Now
                    });
                    db.SaveChanges();
                    Clients.Group(GameId.ToString()).onDisplayMessage(new { message = message, user = user.UserName, timeSent = DateTime.Now });
                }
            }
        }

        [Authorize]
        public void startGame(int GameId)
        {
            var game = db.Games.Where(g => g.Id == GameId).First();
            game.StartGame();
            db.SaveChanges();
            Clients.Group(GameId.ToString()).onStartGame();
        }

        [Authorize]
        public void submitTurn(int GameId, List<Letter> Letters)
        {
            User user = db.Users.Where(u => u.UserName == Context.User.Identity.Name).First();
            var game = db.Games.Include("Players").Include("Players.User").Include("Letters.Player").Include("Letters.Player.User").Where(g => g.Id == GameId).First();
            var player = game.Players.Where(p => p.User.Id == user.Id).First();
            List<string> errors = game.ValidateGameState(Letters, player, db);
            if (errors.Count > 0)
            {
                Clients.Caller.onValidationError(errors);
            }
            else
            {
                game.SubmitTurn(Letters, player, db);
                Clients.Group(GameId.ToString()).onGameStateUpdated();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}