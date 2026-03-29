namespace Bidibip.Plugin.Sdk;

/// <summary>
/// Marks a class as a Bidibip plugin entry point. The host scans loaded assemblies
/// for classes with this attribute that also implement <see cref="IBidibipPlugin"/>.
/// Each plugin DLL must have exactly one class with this attribute.
/// </summary>
/// <example>
/// <code>
/// [BidibipPlugin]
/// public sealed class MyPlugin : IBidibipPlugin { /* ... */ }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BidibipPluginAttribute : Attribute;
