using System.Runtime.Versioning;

// Assembly generation is off in this project, so the platform attribute is missing and the
// platform-compatibility analyzer reports every Windows call here as cross-platform (CA1416). This
// console only exists to talk to the trainer on Windows.
[assembly: SupportedOSPlatform("windows10.0.19041.0")]
