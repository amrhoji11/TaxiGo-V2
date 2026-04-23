using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    [Index(nameof(TokenHash), IsUnique = true)]
    public class RefreshToken
    {

        public int Id { get; set; }

        [Required]
        public string TokenHash { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        public DateTime? RevokedAt { get; set; }

        public string? ReplacedByTokenHash { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

    }
}
