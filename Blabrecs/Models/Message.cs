using System;

namespace Blabrecs.Models
{
    public class Message
    {
        public int Id { get; set; }

        public string Contents { get; set; }

        public DateTime TimeSent { get; set; }

        public User User { get; set; }

        public Game Game { get; set; }
    }
}