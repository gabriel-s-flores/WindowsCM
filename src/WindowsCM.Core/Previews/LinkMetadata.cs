// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Previews;

// Link metadata (Copyous LinkMetadata{title,description,image} parity).
// ImageUrl is the resolved absolute remote URL; the local thumbnail path
// (URL-hash cache) travels separately via LinkImageCache, never in here.
public sealed record LinkMetadata(string? Title, string? Description, string? ImageUrl)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title)
        && string.IsNullOrWhiteSpace(Description)
        && string.IsNullOrWhiteSpace(ImageUrl);
}
