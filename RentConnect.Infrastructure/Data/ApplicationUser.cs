using Microsoft.AspNetCore.Identity;
using RentConnect.Core.Entities;

namespace RentConnect.Data;

public class ApplicationUser : IdentityUser
{
    public Guid? UserProfileId { get; set; }

    public UserProfile? UserProfile { get; set; }
}