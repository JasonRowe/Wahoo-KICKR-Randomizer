using System.Runtime.Versioning;

// This project turns assembly generation off (GenerateAssemblyInfo=false), which also drops the platform
// attribute the platform-compatibility analyzer uses. Without it every WinRT/Windows call in the app is
// reported as "reachable on all platforms" (CA1416) — hundreds of warnings for code that cannot run
// anywhere else. The app requires Windows 10 19041, so say so.
[assembly: SupportedOSPlatform("windows10.0.19041.0")]
