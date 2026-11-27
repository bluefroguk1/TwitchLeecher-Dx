using System;
using System.Collections.Generic;

namespace TwitchLeecher.Core.Models
{
    // A VOD's seek-preview sprite sheets: frame i shows second i * Interval, laid out left to right,
    // top to bottom, Cols x Rows frames of Width x Height pixels per image.
    public record TwitchStoryboard(List<Uri> Images, int Interval, int Cols, int Rows, int Width, int Height, int Count);
}
