using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Entities.DTO.Account.Auth.Register
{
    
        public class TeacherRegisterRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string? PhoneNumber { get; set; }

            public string FullName { get; set; }
            public string Bio { get; set; }
            public string Country { get; set; }
            public decimal HourlyRate { get; set; }
        }
    
}



