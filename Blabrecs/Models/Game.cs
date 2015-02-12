using System;
using System.Collections.Generic;
using System.Linq;

namespace Blabrecs.Models
{
    public class Game
    {
        public Game()
        {
        }

        public bool Active { get; set; }

        public int Id { get; set; }

        public virtual ICollection<Letter> Letters { get; set; }

        public virtual ICollection<Message> Messages { get; set; }

        public string Name { get; set; }

        public int NumberOfPlayers { get; set; }

        public bool Open { get; set; }

        public virtual ICollection<Player> Players { get; set; }

        private BlabrecsContext db { get; set; }

        public void PassTurn(List<Letter> passedLetters, Player player, BlabrecsContext context)
        {
            this.db = context;

            foreach (var passedLetter in passedLetters)
            {
                var letter = db.Letters.Where(l => l.Id == passedLetter.Id).First();
                letter.RackPosition = null;
                player.Letters.Remove(letter);
            }
            DrawNewLetters(player);
            db.SaveChanges();
            UpdatePlayerTurn(player);
            db.SaveChanges();
        }

        public void StartGame()
        {
            this.CloseGame();
            this.InitializeLetters();
            this.DistributeLetters();
            this.ChooseFirstPlayer();
        }

        public void SubmitTurn(List<Letter> playedLetters, Player player, BlabrecsContext context)
        {
            this.db = context;
            this.PlayLetters(playedLetters, player);
            db.SaveChanges();
            this.DrawNewLetters(player);
            db.SaveChanges();
            this.UpdatePlayerTurn(player);
            this.db.SaveChanges();
        }

        public List<string> ValidateGameState(List<Letter> playedLetters, Player player, BlabrecsContext context)
        {
            this.db = context;

            // attach player to letter so that we can distinguish them
            foreach (var letter in playedLetters)
            {
                letter.Player = player;
            }
            var boardLetters = this.Letters.Where(l => l.Row != null && l.Player == null).ToList(); // all existing letters on the board
            int score = 0; // total score for this play
            List<string> errors = new List<string>(); // messages to be returned for display
            List<List<Letter>> words = new List<List<Letter>>(); // array to hold each word formed by the play
            // Determine if the play was vertical or horizontal
            bool vertical = playedLetters.GroupBy(l => l.Column).Count() == 1;
            bool horizontal = playedLetters.GroupBy(l => l.Row).Count() == 1;
            if (!horizontal && !vertical)
            {
                errors.Add("Played letters must be in the same row or column.");
                return errors;
            }
            // if this is the first play on the board then it must be connected ot the middle square
            if (boardLetters.Count() == 0)
            {
                if (!playedLetters.Where(l => l.Row == 7 && l.Column == 7).Any())
                {
                    errors.Add("The first word must contain the middle starting space.");
                    return errors;
                }
            }
            if (horizontal)
            {
                bool isContiguous = HorizontalWordIsContiguous(playedLetters, boardLetters);
                if (!isContiguous)
                {
                    errors.Add("Letters must form one contiguous word.");
                    return errors;
                }
                words = (FindHorizontalWord(boardLetters, playedLetters));
            }
            if (vertical)
            {
                bool isContiguous = VerticalWordIsContiguous(playedLetters, boardLetters);
                if (!isContiguous)
                {
                    errors.Add("Letters must form one contiguous word.");
                    return errors;
                }
                words = (FindVerticalWord(boardLetters, playedLetters));
            }
            bool atLeastOneBoardLetter = false;
            foreach (var word in words.Where(w => w.Count > 1))
            {
                string combinedLetters = "";
                bool wordIsVertical = word.GroupBy(l => l.Column).Count() == 1;
                bool wordIsHorizontal = word.GroupBy(l => l.Row).Count() == 1;
                if (wordIsVertical)
                {
                    foreach (var letter in word.OrderBy(w => w.Row))
                    {
                        if (letter.Player == null)
                        {
                            atLeastOneBoardLetter = true;
                        }
                        if (letter.Type == null)
                        {
                            letter.Type = GetLetterType(letter.Id);
                        }
                        combinedLetters = combinedLetters + letter.Type;
                    }
                }
                if (wordIsHorizontal)
                {
                    foreach (var letter in word.OrderBy(w => w.Column))
                    {
                        if (letter.Player == null)
                        {
                            atLeastOneBoardLetter = true;
                        }
                        if (letter.Type == null)
                        {
                            letter.Type = GetLetterType(letter.Id);
                        }
                        combinedLetters = combinedLetters + letter.Type;
                    }
                }
                if (!atLeastOneBoardLetter && boardLetters.Count() != 0)
                {
                    errors.Add("Must be connected to existing words.");
                    return errors;
                }
                if (!IsWordValid(combinedLetters))
                {
                    errors.Add(string.Format("{0} is not a valid word.", combinedLetters));
                }
                else
                {
                    int wordScore = 0;
                    List<int> wordMultipliers = new List<int>();
                    foreach (var letter in word)
                    {
                        int multiplier;
                        int wordMultiplier;
                        if (letter.Player != null)
                        {
                            multiplier = GetLetterMultiplier(letter.Row.Value, letter.Column.Value);
                        }
                        else
                        {
                            multiplier = 1;
                        }
                        if (letter.Player != null)
                        {
                            wordMultiplier = GetWordMultiplier(letter.Row.Value, letter.Column.Value);
                        }
                        else
                        {
                            wordMultiplier = 1;
                        }
                        wordMultipliers.Add(wordMultiplier);
                        wordScore = wordScore + letter.Value() * multiplier;
                    }
                    // apply all word multipliers that were under played tiles in this word
                    foreach (var multiplier in wordMultipliers)
                    {
                        wordScore = wordScore * multiplier;
                    }
                    score = score + wordScore;
                }
                // Apply bonus for using all player tiles
                if (playedLetters.Count == 7)
                {
                    score = score + 50;
                }
            }
            if (errors.Count == 0)
            {
                player.Score = player.Score + score;
            }
            return errors;
        }

        private static List<List<Letter>> FindHorizontalWord(ICollection<Letter> boardLetters, List<Letter> playedLetters)
        {
            List<List<Letter>> words = new List<List<Letter>>();
            var firstLetter = playedLetters.OrderBy(l => l.Column).First();
            int? currentCol = firstLetter.Column;
            List<Letter> word = new List<Letter>();
            while (currentCol >= 0)
            {
                var connectedLetter = boardLetters.Where(l => l.Column == currentCol && l.Row == firstLetter.Row).FirstOrDefault();
                var playedLetter = playedLetters.Where(l => l.Column == currentCol && l.Row == firstLetter.Row).FirstOrDefault();
                if (connectedLetter == null && playedLetter == null)
                {
                    break;
                }
                else
                {
                    if (connectedLetter != null)
                    {
                        word.Add(connectedLetter);
                        // check to see if this branches to something
                    }
                    else if (playedLetter != null)
                    {
                        word.Add(playedLetter);
                        // check to see if this branches to something
                        if (playedLetters.Count() != 1)
                        {
                            var subword = FindVerticalWord(boardLetters, new List<Letter>() { playedLetter });
                            words = words.Concat(subword).ToList();
                        }
                    }
                    else
                    {
                        throw new Exception("what?");
                    }
                }
                currentCol = currentCol - 1;
            }
            currentCol = firstLetter.Column + 1;
            while (currentCol <= 14)
            {
                var connectedLetter = boardLetters.Where(l => l.Column == currentCol && l.Row == firstLetter.Row).FirstOrDefault();
                var playedLetter = playedLetters.Where(l => l.Column == currentCol && l.Row == firstLetter.Row).FirstOrDefault();
                if (connectedLetter == null && playedLetter == null)
                {
                    break;
                }
                else
                {
                    if (connectedLetter != null)
                    {
                        word.Add(connectedLetter);
                        // check to see if this branches to something
                    }
                    else if (playedLetter != null)
                    {
                        word.Add(playedLetter);
                        // check to see if this branches to something
                        if (playedLetters.Count() != 1)
                        {
                            var subword = FindVerticalWord(boardLetters, new List<Letter>() { playedLetter });
                            words = words.Concat(subword).ToList();
                        }
                    }
                    else
                    {
                        throw new Exception("what?");
                    }
                }
                currentCol = currentCol + 1;
            }
            words.Add(word);
            return words;
        }

        private static List<List<Letter>> FindVerticalWord(ICollection<Letter> boardLetters, List<Letter> playedLetters)
        {
            List<List<Letter>> words = new List<List<Letter>>();
            var firstLetter = playedLetters.OrderBy(l => l.Row).First();
            int? currentRow = firstLetter.Row;
            List<Letter> word = new List<Letter>();
            while (currentRow >= 0)
            {
                var connectedLetter = boardLetters.Where(l => l.Row == currentRow && l.Column == firstLetter.Column).FirstOrDefault();
                var playedLetter = playedLetters.Where(l => l.Row == currentRow && l.Column == firstLetter.Column).FirstOrDefault();
                if (connectedLetter == null && playedLetter == null)
                {
                    break;
                }
                else
                {
                    if (connectedLetter != null)
                    {
                        word.Add(connectedLetter);
                        // check to see if this branches to something
                    }
                    else if (playedLetter != null)
                    {
                        word.Add(playedLetter);
                        // check to see if this branches to something
                        if (playedLetters.Count() != 1)
                        {
                            var subword = FindHorizontalWord(boardLetters, new List<Letter>() { playedLetter });
                            words = words.Concat(subword).ToList();
                        }
                    }
                    else
                    {
                        throw new Exception("what?");
                    }
                }
                currentRow = currentRow - 1;
            }
            currentRow = firstLetter.Row + 1;
            while (currentRow <= 14)
            {
                var connectedLetter = boardLetters.Where(l => l.Row == currentRow && l.Column == firstLetter.Column).FirstOrDefault();
                var playedLetter = playedLetters.Where(l => l.Row == currentRow && l.Column == firstLetter.Column).FirstOrDefault();
                if (connectedLetter == null && playedLetter == null)
                {
                    break;
                }
                else
                {
                    if (connectedLetter != null)
                    {
                        word.Add(connectedLetter);
                        // check to see if this branches to something
                    }
                    else if (playedLetter != null)
                    {
                        word.Add(playedLetter);
                        // check to see if this branches to something
                        if (playedLetters.Count() != 1)
                        {
                            var subword = FindHorizontalWord(boardLetters, new List<Letter>() { playedLetter });
                            words = words.Concat(subword).ToList();
                        }
                    }
                    else
                    {
                        throw new Exception("what?");
                    }
                }
                currentRow = currentRow + 1;
            }
            words.Add(word);
            return words;
        }

        private void ChooseFirstPlayer()
        {
            Random random = new Random();
            int index = random.Next(0, this.Players.Count);
            this.Players.ToArray()[index].IsTurn = true;
        }

        private void CloseGame()
        {
            this.Open = false;
        }

        private void DistributeLetters()
        {
            var letters = this.Letters.ToArray();
            Random random = new Random();
            foreach (var player in this.Players)
            {
                int index = random.Next(0, this.Letters.Count);
                int lettersDrawn = 0;
                while (lettersDrawn < 7)
                {
                    if (letters[index].Player == null)
                    {
                        letters[index].Player = player;
                        letters[index].RackPosition = lettersDrawn + 1;
                        lettersDrawn++;
                    }
                    index = random.Next(0, this.Letters.Count);
                }
            }
        }

        private void DrawNewLetters(Player player)
        {
            var letters = this.Letters.Where(l => l.Player == null && l.Row == null).ToArray();
            int lettersInRackCount = player.Letters.Where(l => l.Row == null).Count();
            int lettersCount = player.Letters.Count();
            var lettersNeeded = (7 - lettersInRackCount);

            var openRackPositions = GetOpenRackPositions(player);
            if (letters.Count() <= lettersNeeded)
            {
                // fewer or equal available letters to be drawn than player needs
                // all remaining tiles go to player
                for (int i = 0; i < letters.Count(); i++)
                {
                    letters[i].Player = player;
                }
            }
            else
            {
                // distribute letters randomly
                Random random = new Random();
                int index = random.Next(0, letters.Count());
                int lettersDrawn = 0;
                while (lettersDrawn < lettersNeeded)
                {
                    if (letters[index].Player == null)
                    {
                        letters[index].Player = player;
                        letters[index].RackPosition = openRackPositions.First();
                        lettersDrawn++;
                        openRackPositions.Remove(openRackPositions.First());
                    }
                    index = random.Next(0, letters.Count());
                }
            }
        }

        private int GetLetterMultiplier(int row, int column)
        {
            switch (row)
            {
                case 0:
                case 14:
                    switch (column)
                    {
                        case 3:
                        case 11:
                            return 2;

                        default:
                            return 1;
                    }
                case 2:
                case 12:
                    switch (column)
                    {
                        case 6:
                        case 8:
                            return 2;

                        default:
                            return 1;
                    }
                case 3:
                case 11:
                    switch (column)
                    {
                        case 0:
                        case 7:
                        case 14:
                            return 2;

                        default:
                            return 1;
                    }
                case 6:
                case 8:
                    switch (column)
                    {
                        case 2:
                        case 6:
                        case 8:
                            return 2;

                        default:
                            return 1;
                    }
                case 7:
                    switch (column)
                    {
                        case 3:
                        case 11:
                            return 2;

                        default:
                            return 1;
                    }
                case 1:
                case 13:
                    switch (column)
                    {
                        case 5:
                        case 9:
                            return 3;

                        default:
                            return 1;
                    }
                case 5:
                case 9:
                    switch (column)
                    {
                        case 1:
                        case 5:
                        case 9:
                        case 13:
                            return 3;

                        default:
                            return 1;
                    }
                default:
                    return 1;
            }
        }

        private string GetLetterType(int LetterId)
        {
            return db.Letters.Where(l => l.Id == LetterId).First().Type;
        }

        private List<int> GetOpenRackPositions(Player player)
        {
            List<int> openRackPositions = new List<int>();
            for (int i = 1; i < 8; i++)
            {
                if (player.Letters.Where(l => l.RackPosition == i).FirstOrDefault() == null)
                {
                    openRackPositions.Add(i);
                }
            }
            return openRackPositions;
        }

        private int GetWordMultiplier(int row, int column)
        {
            switch (row)
            {
                case 0:
                case 14:
                    switch (column)
                    {
                        case 0:
                        case 7:
                        case 14:
                            return 3;

                        default:
                            return 1;
                    }
                case 7:
                    switch (column)
                    {
                        case 0:
                        case 14:
                            return 3;

                        default:
                            return 1;
                    }
                case 1:
                case 13:
                    switch (column)
                    {
                        case 1:
                        case 13:
                            return 2;

                        default:
                            return 1;
                    }
                case 2:
                case 12:
                    switch (column)
                    {
                        case 2:
                        case 12:
                            return 2;

                        default:
                            return 1;
                    }
                case 3:
                case 11:
                    switch (column)
                    {
                        case 3:
                        case 11:
                            return 2;

                        default:
                            return 1;
                    }
                case 4:
                case 10:
                    switch (column)
                    {
                        case 4:
                        case 10:
                            return 2;

                        default:
                            return 1;
                    }
                default:
                    return 1;
            }
        }

        private bool HorizontalWordIsContiguous(List<Letter> playedLetters, List<Letter> boardLetters)
        {
            int? currentColumn = null;
            foreach (var letter in playedLetters.OrderBy(l => l.Column))
            {
                if (currentColumn == null)
                {
                    currentColumn = letter.Column;
                }
                else
                {
                    if (letter.Column == currentColumn + 1)
                    {
                        currentColumn = letter.Column;
                    }
                    else
                    {
                        while (currentColumn < letter.Column - 1)
                        {
                            if (boardLetters.Where(l => l.Row == letter.Row && l.Column == currentColumn + 1).Any())
                            {
                                currentColumn++;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        currentColumn = letter.Column;
                    }
                }
            }
            return true;
        }

        private void InitializeLetters()
        {
            for (int i = 0; i < 12; i++)
            {
                this.Letters.Add(new Letter() { Type = "E" });
            }
            for (int i = 0; i < 9; i++)
            {
                this.Letters.Add(new Letter() { Type = "A" });
            }
            for (int i = 0; i < 9; i++)
            {
                this.Letters.Add(new Letter() { Type = "I" });
            }
            for (int i = 0; i < 8; i++)
            {
                this.Letters.Add(new Letter() { Type = "O" });
            }
            for (int i = 0; i < 6; i++)
            {
                this.Letters.Add(new Letter() { Type = "N" });
            }
            for (int i = 0; i < 6; i++)
            {
                this.Letters.Add(new Letter() { Type = "R" });
            }
            for (int i = 0; i < 6; i++)
            {
                this.Letters.Add(new Letter() { Type = "T" });
            }
            for (int i = 0; i < 4; i++)
            {
                this.Letters.Add(new Letter() { Type = "L" });
            }
            for (int i = 0; i < 4; i++)
            {
                this.Letters.Add(new Letter() { Type = "S" });
            }
            for (int i = 0; i < 4; i++)
            {
                this.Letters.Add(new Letter() { Type = "U" });
            }
            for (int i = 0; i < 4; i++)
            {
                this.Letters.Add(new Letter() { Type = "D" });
            }
            for (int i = 0; i < 3; i++)
            {
                this.Letters.Add(new Letter() { Type = "G" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "B" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "C" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "M" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "P" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "F" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "H" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "V" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "W" });
            }
            for (int i = 0; i < 2; i++)
            {
                this.Letters.Add(new Letter() { Type = "Y" });
            }
            this.Letters.Add(new Letter() { Type = "K" });
            this.Letters.Add(new Letter() { Type = "J" });
            this.Letters.Add(new Letter() { Type = "X" });
            this.Letters.Add(new Letter() { Type = "Q" });
            this.Letters.Add(new Letter() { Type = "Z" });
        }

        private bool IsWordValid(string combinedLetters)
        {
            var word = db.Dictionary.Where(d => d.Word == combinedLetters).FirstOrDefault();
            return word != null;
        }

        private void PlayLetters(List<Letter> Letters, Player player)
        {
            foreach (var letter in player.Letters)
            {
                var playedLetter = Letters.Where(l => l.Id == letter.Id).FirstOrDefault();
                if (playedLetter == null)
                {
                    continue;
                }
                else
                {
                    letter.Row = playedLetter.Row;
                    letter.Column = playedLetter.Column;
                    letter.Player = null;
                    letter.RackPosition = null;
                }
            }
        }

        private void UpdatePlayerTurn(Player player)
        {
            var players = this.Players.ToArray();
            for (int i = 0; i < players.Count(); i++)
            {
                if (players[i].Id == player.Id)
                {
                    players[i].IsTurn = false;
                    if (i == players.Count() - 1)
                    {
                        players[0].IsTurn = true;
                    }
                    else
                    {
                        players[i + 1].IsTurn = true;
                    }
                }
            }
            db.SaveChanges();
        }

        private bool VerticalWordIsContiguous(List<Letter> playedLetters, List<Letter> boardLetters)
        {
            int? currentRow = null;
            foreach (var letter in playedLetters.OrderBy(l => l.Row))
            {
                if (currentRow == null)
                {
                    currentRow = letter.Row;
                }
                else
                {
                    if (letter.Row == currentRow + 1)
                    {
                        currentRow = letter.Row;
                    }
                    else
                    {
                        while (currentRow < letter.Row - 1)
                        {
                            if (boardLetters.Where(l => l.Column == letter.Column && l.Row == currentRow + 1).Any())
                            {
                                currentRow++;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        currentRow = letter.Row;
                    }
                }
            }
            return true;
        }
    }
}