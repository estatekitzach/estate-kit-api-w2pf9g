using System;

namespace EstateKit.Core.Attributes
{
    /// <summary>
    /// Attribute to specify the database table name for a class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TableAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableAttribute"/> class.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
