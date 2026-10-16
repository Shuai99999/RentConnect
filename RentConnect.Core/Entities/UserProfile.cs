using System;
using System.Collections.Generic;
using System.Text;

namespace RentConnect.Core.Entities;

public sealed class UserProfile
{
    private UserProfile()
    {

    }

    public UserProfile(Guid id, string displayName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "User profile ID cannot be empty.",
                nameof(id));
        }

        Id = id;
        SetDisplayName(displayName);
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public void Rename(string displayName)
    {
        SetDisplayName(displayName);
    }

    private void SetDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Display name is required.",
                nameof(displayName));
        }
        DisplayName = displayName.Trim();
    }
}
