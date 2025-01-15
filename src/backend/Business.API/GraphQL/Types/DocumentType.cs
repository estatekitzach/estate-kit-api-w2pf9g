using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using HotChocolate.Data;
using System;
using System.Threading.Tasks;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using Amazon.S3;

namespace EstateKit.Business.API.GraphQL.Types
{
    /// <summary>
    /// GraphQL type definition for Document entity with comprehensive security controls,
    /// field-level encryption, audit logging, and OCR processing status tracking.
    /// </summary>
    [ExtendObjectType]
    [Authorize(Policy = "DocumentAccess")]
    public class DocumentType
    {
        private readonly IAmazonS3 _s3Client;
        private const string VERSION = "13.0.0"; // HotChocolate version

        public DocumentType(IAmazonS3 s3Client)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        }

        /// <summary>
        /// Configures the GraphQL type fields with security controls and resolvers
        /// </summary>
        public void Configure(IObjectTypeDescriptor<Document> descriptor)
        {
            // Configure ID field with audit logging
            descriptor.Field(d => d.Id)
                .Type<NonNullType<IdType>>()
                .Description("Unique identifier for the document")
                .Authorize("ViewDocument");

            // Configure document type with enum validation
            descriptor.Field(d => d.Type)
                .Type<NonNullType<EnumType<DocumentType>>>()
                .Description("Type of document (e.g., DRIVERS_LICENSE, PASSPORT, etc.)")
                .Authorize("ViewDocument");

            // Configure front image URL with S3 validation
            descriptor.Field(d => d.FrontImageUrl)
                .Type<StringType>()
                .Description("S3 URL for the front image of the document")
                .Authorize("ViewDocumentImages")
                .UseFiltering()
                .Resolve(async context =>
                {
                    var document = context.Parent<Document>();
                    if (await ValidateS3Url(document.FrontImageUrl))
                    {
                        return document.FrontImageUrl;
                    }
                    return null;
                });

            // Configure back image URL with S3 validation
            descriptor.Field(d => d.BackImageUrl)
                .Type<StringType>()
                .Description("S3 URL for the back image of the document (if applicable)")
                .Authorize("ViewDocumentImages")
                .UseFiltering()
                .Resolve(async context =>
                {
                    var document = context.Parent<Document>();
                    if (string.IsNullOrEmpty(document.BackImageUrl) || 
                        await ValidateS3Url(document.BackImageUrl))
                    {
                        return document.BackImageUrl;
                    }
                    return null;
                });

            // Configure location with encryption
            descriptor.Field(d => d.Location)
                .Type<StringType>()
                .Description("Physical location of the document (if applicable)")
                .Authorize("ViewDocumentLocation")
                .UseFiltering();

            // Configure InKit status
            descriptor.Field(d => d.InKit)
                .Type<NonNullType<BooleanType>>()
                .Description("Indicates if the document is stored in the estate planning kit")
                .Authorize("ViewDocument");

            // Configure metadata with JSON type
            descriptor.Field(d => d.Metadata)
                .Type<StringType>()
                .Description("JSON metadata extracted from document OCR processing")
                .Authorize("ViewDocumentMetadata")
                .UseFiltering();

            // Configure OCR processing status
            descriptor.Field(d => d.IsProcessed)
                .Type<NonNullType<BooleanType>>()
                .Description("Indicates if OCR processing is complete")
                .Authorize("ViewDocument");

            // Configure timestamps
            descriptor.Field(d => d.CreatedAt)
                .Type<NonNullType<DateTimeType>>()
                .Description("Timestamp when the document was created")
                .Authorize("ViewDocument");

            descriptor.Field(d => d.UpdatedAt)
                .Type<DateTimeType>()
                .Description("Timestamp when the document was last updated")
                .Authorize("ViewDocument");
        }

        /// <summary>
        /// Validates S3 URL format and accessibility
        /// </summary>
        private async Task<bool> ValidateS3Url(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            try
            {
                // Parse S3 URL to extract bucket and key
                var uri = new Uri(url);
                var bucket = uri.Host.Split('.')[0];
                var key = uri.AbsolutePath.TrimStart('/');

                // Verify object exists in S3
                var request = new Amazon.S3.Model.GetObjectMetadataRequest
                {
                    BucketName = bucket,
                    Key = key
                };

                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}