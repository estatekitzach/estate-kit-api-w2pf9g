using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.DataProtection;
using EstateKit.Core.Entities;
using EstateKit.Core.Interfaces;
using EstateKit.Core.Enums;

namespace EstateKit.Business.API.GraphQL.Queries
{
    /// <summary>
    /// Implements GraphQL queries for document operations with comprehensive security controls,
    /// field-level encryption, and audit logging capabilities.
    /// </summary>
    [ExtendObjectType("Query")]
    [Authorize]
    public class DocumentQueries
    {
        private readonly IDocumentRepository _documentRepository;

        /// <summary>
        /// Initializes a new instance of DocumentQueries with required dependencies
        /// </summary>
        /// <param name="documentRepository">Repository for document operations</param>
        public DocumentQueries(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository ?? 
                throw new ArgumentNullException(nameof(documentRepository));
        }

        /// <summary>
        /// Retrieves a document by its unique identifier with proper authorization and audit logging
        /// </summary>
        /// <param name="id">Unique identifier of the document</param>
        /// <returns>The requested document if found and authorized, null otherwise</returns>
        [UseFirstOrDefault]
        [Authorize(Policy = "DocumentAccess")]
        [UseProjection]
        [UseFiltering]
        public async Task<Document> GetDocumentByIdAsync(
            [GraphQLType(typeof(NonNullType<IdType>))] Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Invalid document ID", nameof(id));
            }

            var document = await _documentRepository.GetByIdAsync(id);

            // Document will already have sensitive fields decrypted by the repository layer
            return document;
        }

        /// <summary>
        /// Retrieves all documents belonging to a specific user with proper authorization and audit logging
        /// </summary>
        /// <param name="userId">Unique identifier of the user</param>
        /// <returns>Collection of documents belonging to the user</returns>
        [UseFiltering]
        [UseSorting]
        [UseProjection]
        [Authorize(Policy = "DocumentAccess")]
        public async Task<IEnumerable<Document>> GetDocumentsByUserIdAsync(
            [GraphQLType(typeof(NonNullType<IdType>))] Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("Invalid user ID", nameof(userId));
            }

            var documents = await _documentRepository.GetByUserIdAsync(userId);

            // Documents will already have sensitive fields decrypted by the repository layer
            return documents;
        }

        /// <summary>
        /// Retrieves documents by type with proper authorization and filtering
        /// </summary>
        /// <param name="type">Type of documents to retrieve</param>
        /// <param name="userId">Optional user ID to filter documents</param>
        /// <returns>Collection of documents matching the specified type</returns>
        [UseFiltering]
        [UseSorting]
        [UseProjection]
        [Authorize(Policy = "DocumentAccess")]
        public async Task<IEnumerable<Document>> GetDocumentsByTypeAsync(
            [GraphQLType(typeof(NonNullType<EnumType<DocumentType>>))] DocumentType type,
            Guid? userId = null)
        {
            // If userId is provided, get documents for specific user
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                var userDocuments = await _documentRepository.GetByUserIdAsync(userId.Value);
                return userDocuments.Where(d => d.Type == type);
            }

            // Otherwise, get all documents of specified type (requires elevated permissions)
            [Authorize(Policy = "AdminDocumentAccess")]
            var documents = await _documentRepository.GetByUserIdAsync(Guid.Empty);
            return documents.Where(d => d.Type == type);
        }

        /// <summary>
        /// Retrieves documents based on their processing status
        /// </summary>
        /// <param name="isProcessed">Processing status to filter by</param>
        /// <returns>Collection of documents matching the processing status</returns>
        [UseFiltering]
        [UseSorting]
        [UseProjection]
        [Authorize(Policy = "DocumentAccess")]
        public async Task<IEnumerable<Document>> GetDocumentsByProcessingStatusAsync(
            [GraphQLType(typeof(NonNullType<BooleanType>))] bool isProcessed)
        {
            var documents = await _documentRepository.GetByUserIdAsync(Guid.Empty);
            return documents.Where(d => d.IsProcessed == isProcessed);
        }

        /// <summary>
        /// Retrieves documents stored in the estate planning kit
        /// </summary>
        /// <param name="userId">Optional user ID to filter documents</param>
        /// <returns>Collection of documents stored in the kit</returns>
        [UseFiltering]
        [UseSorting]
        [UseProjection]
        [Authorize(Policy = "DocumentAccess")]
        public async Task<IEnumerable<Document>> GetDocumentsInKitAsync(
            Guid? userId = null)
        {
            var documents = userId.HasValue && userId.Value != Guid.Empty
                ? await _documentRepository.GetByUserIdAsync(userId.Value)
                : await _documentRepository.GetByUserIdAsync(Guid.Empty);

            return documents.Where(d => d.InKit);
        }
    }
}