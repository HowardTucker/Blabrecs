using System.ComponentModel.DataAnnotations;

namespace Blabrecs.Models
{
    public class Dictionary
    {
        [Key]
        public string Word { get; set; }
    }
}