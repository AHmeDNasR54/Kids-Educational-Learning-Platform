using Ecommerce.Entities.Models.Auth.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Entities.Models.Auth.Users
{
    public class Teacher
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Bio { get; set; }
        public string Country { get; set; }
        public decimal HourlyRate { get; set; } = 0;
        public bool IsVerified { get; set; } = true;
        public DateTime JoinDate { get; set; }=DateTime.Now;
        public string UserId { get; set; }

        public User User { get; set; }
       
    }
}
