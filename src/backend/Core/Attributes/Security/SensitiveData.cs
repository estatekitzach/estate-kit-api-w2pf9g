using System;

namespace EstateKit.Core.Attributes.Security
{
    /// <summary>
    /// Attribute to indicate that a property contains sensitive data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class SensitiveDataAttribute : Attribute
    {
    }
}
