// InternalsVisibleTo.cs
//
// Grants the companion test assembly access to internal members, following
// the CodeBrix family convention (upstream: FriendAssemblies.cs).

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo ("Pinta.Brix.Effects.Tests")]
