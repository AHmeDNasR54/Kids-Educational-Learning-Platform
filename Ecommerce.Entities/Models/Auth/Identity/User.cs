using Microsoft.AspNetCore.Identity;
using Ecommerce.Entities.Models.Auth.Users;

namespace Ecommerce.Entities.Models.Auth.Identity
{
    public class User : IdentityUser
    {
            public Parent Parent { get; set; }
            public Teacher Teacher { get; set; }

    }
}
