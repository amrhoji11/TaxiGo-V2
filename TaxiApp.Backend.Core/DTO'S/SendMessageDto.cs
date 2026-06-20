using System.ComponentModel.DataAnnotations;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class SendMessageDto
    {
        [Required]
        public int OrderId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(1000)]
        public string Body { get; set; }
    }
}
