// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Capture;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
