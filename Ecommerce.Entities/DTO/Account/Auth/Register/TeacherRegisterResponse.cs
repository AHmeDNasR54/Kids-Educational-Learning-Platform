using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Entities.DTO.Account.Auth.Register
{
        public class TeacherRegisterResponse
        {
            public string Id { get; set; }
            public string Email { get; set; }
            public string? PhoneNumber { get; set; }
            public bool IsEmailConfirmed { get; set; }

            public string Role { get; set; }
            public string FullName { get; set; }
            public string Bio { get; set; }
            public string Country { get; set; }
            public decimal HourlyRate { get; set; }

            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
        }
    
}



