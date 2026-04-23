using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxiApp.Backend.Core.DTO_S
{
    public class UsersResponseDto
    {
        public int Count { get; set; }
        public IEnumerable<UserListDto> Users { get; set; }
    }
}
