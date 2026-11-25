using System.Runtime.InteropServices;

[assembly: ComVisible(false)]

// Decal keeps track of plugins by this guid (HKLM\SOFTWARE\Decal\Plugins\{guid}). Keep it unique and don't change it.
[assembly: Guid("fa1952c0-b6cd-42a1-ae01-ad12f0120a4c")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Horizon.Tests")]
