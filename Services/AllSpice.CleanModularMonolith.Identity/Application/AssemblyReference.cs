using System.Reflection;

namespace AllSpice.CleanModularMonolith.Identity.Application;

/// <summary>
/// Marker type used to scan this module's Application assembly for validators
/// and message handlers. Not part of the public surface.
/// </summary>
internal static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}


