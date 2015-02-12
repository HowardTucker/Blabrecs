using System.Collections.Generic;

namespace Blabrecs.Models
{
    public class Player
    {
        public int Id { get; set; }

        public virtual Game Game { get; set; }

        public virtual User User { get; set; }

        public virtual ICollection<Letter> Letters { get; set; }

        public int PlayerNumber { get; set; }

        public int Score { get; set; }

        public bool IsTurn { get; set; }
    }
}