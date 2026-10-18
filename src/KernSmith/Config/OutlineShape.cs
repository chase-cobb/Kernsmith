namespace KernSmith;

/// <summary>
/// Corner shape of the glyph outline. Mostly visible with <see cref="AntiAliasMode.None"/>,
/// where the outline is a hard 0/255 threshold.
/// </summary>
public enum OutlineShape
{
    /// <summary>Euclidean distance: rounded corners (default).</summary>
    Round,

    /// <summary>Chebyshev distance (<c>max(|dx|, |dy|) &lt;= N</c>): square corners, diagonals filled.</summary>
    Square,

    /// <summary>Manhattan distance (<c>|dx| + |dy| &lt;= N</c>): diamond/plus-shaped corners.</summary>
    Plus
}
