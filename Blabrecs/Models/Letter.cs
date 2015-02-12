using System;

namespace Blabrecs.Models
{
    public class Letter
    {
        public int Id { get; set; }

        public int? Row { get; set; }

        public int? Column { get; set; }

        public int? RackPosition { get; set; }

        public virtual Game Game { get; set; }

        public virtual Player Player { get; set; }

        public string Type { get; set; }

        public int Value()
        {
            switch (this.Type)
            {
                case "E":
                case "A":
                case "I":
                case "N":
                case "O":
                case "R":
                case "S":
                case "T":
                case "U":
                case "L":
                    return 1;

                case "D":
                case "M":
                case "G":
                    return 2;

                case "B":
                case "C":
                case "P":
                    return 3;

                case "F":
                case "H":
                case "V":
                    return 4;

                case "J":
                case "Q":
                    return 8;

                case "K":
                case "W":
                case "X":
                case "Y":
                case "Z":
                    return 10;

                default:
                    throw new Exception("Could not assign score due to error");
            }
        }
    }
}