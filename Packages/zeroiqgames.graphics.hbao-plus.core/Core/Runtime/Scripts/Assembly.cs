
// Give access to internals of core assembly to HBAO+ HDRP/URP assemblies 

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ZeroIQGames.Graphics.HBAOPlus.URP.Runtime")]
[assembly: InternalsVisibleTo("ZeroIQGames.Graphics.HBAOPlus.HDRP.Runtime")]
[assembly: InternalsVisibleTo("ZeroIQGames.Graphics.HBAOPlus.URP.Editor")]
[assembly: InternalsVisibleTo("ZeroIQGames.Graphics.HBAOPlus.HDRP.Editor")]