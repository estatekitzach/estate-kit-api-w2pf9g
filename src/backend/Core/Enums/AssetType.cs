using System; // Version 9.0.0 - Core .NET functionality for enum definition

namespace EstateKit.Core.Enums;

/// <summary>
/// Comprehensive enumeration of supported asset types in the estate planning system,
/// providing standardized categorization for all physical assets and property.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Real estate property assets including residential homes, commercial buildings,
    /// land parcels, and investment properties
    /// </summary>
    REALESTATE,

    /// <summary>
    /// Motorized vehicles including cars, trucks, motorcycles, boats, aircraft,
    /// and recreational vehicles
    /// </summary>
    VEHICLE,

    /// <summary>
    /// Bank safety deposit boxes containing valuable items, documents,
    /// or other secured possessions
    /// </summary>
    SAFETYDEPOSITBOX,

    /// <summary>
    /// Valuable jewelry items including precious stones, watches,
    /// and other wearable valuables
    /// </summary>
    JEWELRY,

    /// <summary>
    /// Art pieces including paintings, sculptures, photographs,
    /// and other artistic works of value
    /// </summary>
    ARTWORK,

    /// <summary>
    /// Collectible items of value including stamps, coins, memorabilia,
    /// and other collection pieces
    /// </summary>
    COLLECTIBLE,

    /// <summary>
    /// Registered firearms, weapons, and related accessories
    /// requiring special documentation
    /// </summary>
    FIREARM,

    /// <summary>
    /// Assets related to business ownership including equipment,
    /// inventory, and intellectual property
    /// </summary>
    BUSINESSASSET,

    /// <summary>
    /// Precious metals including gold, silver, platinum,
    /// and other valuable metal holdings
    /// </summary>
    PRECIOUSMETAL,

    /// <summary>
    /// Valuable equipment and machinery including tools,
    /// appliances, and specialized devices
    /// </summary>
    EQUIPMENT,

    /// <summary>
    /// Other asset types not covered by specific categories,
    /// providing extensibility for future additions
    /// </summary>
    OTHER
}
