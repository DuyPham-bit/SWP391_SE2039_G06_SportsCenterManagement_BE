using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.Common;

/// <summary>Wrong OTP. RemainingAttempts == 0 means the code is now locked and a new one must be issued.</summary>
public sealed class OtpAttemptException(string message, int remainingAttempts) : ValidationException(message)
{
    public int RemainingAttempts { get; } = remainingAttempts;
}
