using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.Models
{
    public enum SystemMode
    {
        Auto = 0,
        Manual = 1
    }
    public class SystemSettings
    {
        [Key]
        public int Id { get; set; }
        public SystemMode Mode { get; set; }
    }
}
