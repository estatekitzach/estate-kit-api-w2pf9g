using System;

namespace EstateKit.Core.Attributes.Audit
{
    /// <summary>
    /// Attribute to indicate that a class should be audited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class AuditableAttribute : System.Attribute
    {
    }
}
