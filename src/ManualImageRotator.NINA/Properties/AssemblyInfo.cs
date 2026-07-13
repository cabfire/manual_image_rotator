using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Manual Image Rotator")]
[assembly: AssemblyDescription("Guided manual camera rotation for N.I.N.A. using live image-based field rotation measurements.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Fabrice Houvet")]
[assembly: AssemblyProduct("Manual Image Rotator")]
[assembly: AssemblyCopyright("Copyright 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyMetadata("License", "MIT")]
[assembly: AssemblyMetadata("LicenseURL", "")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/cabfire/manual_image_rotator")]
[assembly: AssemblyMetadata("Repository", "https://github.com/cabfire/manual_image_rotator")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/cabfire/manual_image_rotator/commits/main")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/cabfire/manual_image_rotator/main/docs/images/manual-image-rotator-logo.png")]
[assembly: AssemblyMetadata("Tags", "rotator,manual,image-rotation")]
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.0")]
[assembly: AssemblyMetadata("LongDescription",
    "Manual Image Rotator is a guided manual rotator assistant for N.I.N.A. It does not require a motorized rotator: " +
    "it uses the camera already connected in N.I.N.A., captures live frames, detects stars, measures the actual field rotation, " +
    "and updates a virtual rotator position while you physically rotate the camera.\n\n" +
    "The guidance window shows the current angle, the requested target angle, a live blue needle, the number of matched stars, " +
    "and a color-coded quality indicator. When the target is reached within the configured tolerance, N.I.N.A. can complete the move " +
    "like it would with a regular rotator.\n\n" +
    "Frame rejection\n\n" +
    "Each new frame is checked before it can update the displayed rotator angle. Frames can be rejected when they have too few matched stars, " +
    "low measurement quality, or a sudden angle jump from the last accepted position. Rejected frames still display their metrics and status, " +
    "but the current angle and blue needle stay on the last accepted measurement.\n\n" +
    "Detection zone\n\n" +
    "The plugin detects stars in a central annular zone of the image. The outer radius is half of the shortest image side, " +
    "while the central area can be excluded to avoid unstable stars near the rotation center. Candidate stars are sorted by brightness, " +
    "then filtered so retained stars remain separated from each other. This helps avoid selecting several local maxima from the same bright star.\n\n" +
    "![Annular detection zone](https://raw.githubusercontent.com/cabfire/manual%5Fimage%5Frotator/main/docs/images/annular%5Fdetection%5Fzone%5Fplugin.png)")]

[assembly: ComVisible(false)]
[assembly: Guid("d50ae298-63e4-4e95-9f7d-ea5d0ba8d21b")]

[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
