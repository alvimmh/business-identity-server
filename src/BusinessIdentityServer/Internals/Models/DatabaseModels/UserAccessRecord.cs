using Microsoft.EntityFrameworkCore;

namespace BusinessIdentityServer.Internals.Models.DatabaseModels;

// Class representing records of user data access by administrative users
[Index(nameof(UserId)), Index(nameof(AccessorId))]
public class UserAccessRecord
{
    public string Id { get; private set; }
    public string Purpose { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string UserId { get; set; } = null!;
    public string AccessorId { get; set; } = null!;

    public UserAccessRecord()
    {
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
    }
}
