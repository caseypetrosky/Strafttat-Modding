// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Reflection;

namespace StraftatModding
{
    /// <summary>
    /// Reads a static member that may be declared as either a field or a
    /// property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity singletons get written both ways and the difference is invisible at
    /// the call site: <c>SteamLobby.Instance</c> reads identically in C# whether
    /// it is a field or a property. STRAFTAT declares all of them as plain
    /// fields - <c>public static SteamLobby Instance;</c>, likewise
    /// <c>ScoreManager</c> and <c>GameManager</c>.
    /// </para>
    /// <para>
    /// Reflection does not forgive that. <c>AccessTools.Property</c> returns
    /// null for a field, so a bridge that looked only for a property resolved
    /// nothing, decided the game had changed, and stood down - while compiling
    /// perfectly and logging a plausible-sounding warning. Asking for "the
    /// static member called X", rather than guessing which kind it is, removes
    /// the whole failure mode.
    /// </para>
    /// </remarks>
    internal static class StaticAccess
    {
        /// <summary>
        /// A getter for the named static member, or null if no field or
        /// property by that name exists.
        /// </summary>
        public static Func<object> Getter(Type owner, string name)
        {
            if (owner == null || string.IsNullOrEmpty(name)) return null;

            // Plain reflection rather than Harmony's AccessTools: this helper
            // then carries no dependencies, which is what lets it be tested
            // outside the game.
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.Static | BindingFlags.FlattenHierarchy;

            // Walk the base chain by hand. FlattenHierarchy does not surface
            // private statics declared on a base type.
            for (var type = owner; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, flags);
                if (field != null && field.IsStatic) return () => field.GetValue(null);

                var property = type.GetProperty(name, flags);
                var getter = property?.GetGetMethod(nonPublic: true);
                if (getter != null && getter.IsStatic) return () => property.GetValue(null);
            }

            return null;
        }

        /// <summary>
        /// Convenience for the common case: read it now, or null.
        /// </summary>
        public static object Read(Type owner, string name) => Getter(owner, name)?.Invoke();
    }
}
