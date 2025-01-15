using HotChocolate; // Version 13.0.0
using HotChocolate.Types; // Version 13.0.0
using HotChocolate.Authorization; // Version 13.0.0
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using System;
using System.Globalization;

namespace EstateKit.Business.API.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for Asset entities with comprehensive security controls 
    /// and field-level encryption support.
    /// </summary>
    [ExtendObjectType]
    [Authorize(Policy = "AssetAccess")]
    public class AssetType
    {
        public void Configure(IObjectTypeDescriptor<Asset> descriptor)
        {
            // Configure non-nullable fields with appropriate security controls
            descriptor.Field(a => a.Id)
                .Type<NonNullType<IdType>>()
                .Description("Unique identifier for the asset");

            descriptor.Field(a => a.UserId)
                .Type<NonNullType<IdType>>()
                .Authorize(new[] { "AssetOwner", "Administrator" })
                .Description("Reference to the asset owner");

            descriptor.Field(a => a.Name)
                .Type<NonNullType<StringType>>()
                .Description("Name or title of the asset");

            descriptor.Field(a => a.Description)
                .Description("Detailed description of the asset");

            descriptor.Field(a => a.Type)
                .Type<NonNullType<EnumType<AssetType>>>()
                .Description("Categorization of the asset type");

            // Encrypted fields with additional authorization
            descriptor.Field(a => a.Location)
                .Type<NonNullType<StringType>>()
                .Authorize(new[] { "AssetOwner", "Administrator" })
                .Description("Physical location or storage location of the asset (encrypted)");

            descriptor.Field(a => a.EstimatedValue)
                .Type<DecimalType>()
                .Authorize(new[] { "AssetOwner", "Administrator" })
                .Description("Estimated monetary value of the asset");

            descriptor.Field(a => a.AccessInformation)
                .Type<StringType>()
                .Authorize(new[] { "AssetOwner", "Administrator" })
                .Description("Encrypted access information for the asset");

            descriptor.Field(a => a.IsActive)
                .Type<NonNullType<BooleanType>>()
                .Description("Indicates if the asset is currently active");

            descriptor.Field(a => a.CreatedAt)
                .Type<NonNullType<DateTimeType>>()
                .Description("Timestamp of asset creation");

            // Computed fields
            descriptor.Field("formattedValue")
                .Type<StringType>()
                .Resolve(context => ResolveFormattedValue(context.Parent<Asset>()))
                .Description("Formatted estimated value with currency symbol");

            descriptor.Field("ageInDays")
                .Type<IntType>()
                .Resolve(context => ResolveAssetAge(context.Parent<Asset>()))
                .Description("Age of the asset in days since creation");

            // Error handling
            descriptor.Field(a => a.Id)
                .Error<ArgumentException>()
                .Error<UnauthorizedAccessException>();
        }

        /// <summary>
        /// Resolves the formatted estimated value with currency symbol
        /// </summary>
        private string ResolveFormattedValue(Asset asset)
        {
            if (asset.EstimatedValue == 0)
            {
                return "Not Specified";
            }

            return asset.EstimatedValue.ToString("C", 
                CultureInfo.CreateSpecificCulture("en-US"));
        }

        /// <summary>
        /// Computes the age of the asset from creation date
        /// </summary>
        private int ResolveAssetAge(Asset asset)
        {
            return (int)(DateTime.UtcNow - asset.CreatedAt).TotalDays;
        }
    }
}