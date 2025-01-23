using System;

namespace EstateKit.Core.Attributes.Security
{
    /// <summary>
    /// Attribute to indicate that a property should be encrypted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class EncryptedAttribute : Attribute
    {
    }
}
