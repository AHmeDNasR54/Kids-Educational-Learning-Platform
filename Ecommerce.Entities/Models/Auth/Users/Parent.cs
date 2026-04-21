using Ecommerce.Entities.Models.Auth.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Entities.Models.Auth.Users
{
    public class Parent
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ProfileImageUrl { get; set; } = null;
        public bool IsActive { get; set; } = true;
        public string UserId { get; set; }
        public User User { get; set; }
        
    }
}
