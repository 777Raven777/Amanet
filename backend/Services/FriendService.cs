using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using backend.Models.DTO;

namespace backend.Services
{
    public class FriendService
    {
        private readonly AppDbContext _context;

        public FriendService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, string Message)> SendRequest(Guid userId, Guid callerId)
        {
            try
            {
                if (userId == callerId)
                {
                    return (false, "You cannot send a friend request to yourself.");
                }

                bool userExists = await _context.Users.AnyAsync(x => x.Id == userId);
                if (!userExists)
                {
                    return (false, "The requested user does not exist.");
                }

                bool existingRelation = await _context.Relationships.AnyAsync(x =>
                    (x.SenderId == callerId && x.ReceiverId == userId) ||
                    (x.SenderId == userId && x.ReceiverId == callerId));

                if (existingRelation)
                {
                    return (false, "A relationship with this user already exists.");
                }

                Relationship relationship = new Relationship
                {
                    ReceiverId = userId,
                    SenderId = callerId,
                    Status = RelationshipType.Waiting,
                };

                _context.Relationships.Add(relationship);
                await _context.SaveChangesAsync();
                return (true, "Request was sent successfully.");
            }
            catch (Exception ex)
            {
                // log error here
                return (false, "An internal server error occurred while sending the request.");
            }
        }

        public async Task<(bool Success, string Message)> RejectRequest(Guid relationshipId, Guid callerId)
        {
            try
            {
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE ""Relationships""
                    SET Status = {(int)RelationshipType.Rejected} 
                    WHERE Id = {relationshipId} 
                      AND ReceiverId = {callerId} 
                      AND Status = {(int)RelationshipType.Waiting}");

                if (rows > 0)
                {
                    return (true, "Successfully rejected request");
                }
                else
                {
                    return (false, "Request does not exist");
                }
            }
            catch (Exception es)
            {
                // log error
                return (false, "An internal server error occurred while sending the request.");
            }
        }

        public async Task<PaginatedRequestListDTO> GetReceivedRequests(Guid callerId, int currentPage, int pageSize)
        {
            var relationships = await _context.Relationships
                .Where(x => x.ReceiverId == callerId && x.Status == RelationshipType.Waiting)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new RelationshipDTO
                {
                    Id = x.Id,
                    Status = x.Status,
                    Sender = new UserDTO { Id = x.Sender.Id, Username = x.Sender.Username, ProfilePictureUrl = x.Sender.ProfilePictureUrl },
                })
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize+1)
                .ToListAsync();

            bool nextPage = relationships.Count > pageSize;
            if (nextPage)
            {
                relationships.RemoveAt(relationships.Count);
            }

            return new PaginatedRequestListDTO
            {
                PageSize = pageSize,
                CurrentPage = currentPage,
                NextPage = nextPage,
                Relationships = relationships,
            };
        }

        public async Task<PaginatedRequestListDTO> GetSentRequests(Guid callerId, int currentPage, int pageSize)
        {
            var relationships = await _context.Relationships
                .Where(x => x.SenderId == callerId && x.Status == RelationshipType.Waiting)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new RelationshipDTO
                {
                    Id = x.Id,
                    Status = x.Status,
                    Receiver = new UserDTO { Id = x.Receiver.Id, Username = x.Receiver.Username, ProfilePictureUrl = x.Receiver.ProfilePictureUrl },
                })
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            bool nextPage = relationships.Count > pageSize;
            if (nextPage)
            {
                relationships.RemoveAt(relationships.Count);
            }

            return new PaginatedRequestListDTO
            {
                PageSize = pageSize,
                CurrentPage = currentPage,
                NextPage = nextPage,
                Relationships = relationships,
            };
        }

        public async Task<List<FriendDTO>> RetrieveActiveRelationship(Guid callerId, int currentPage, int pageSize)
        {
            return await _context.Relationships
                .Where(x => (x.SenderId == callerId || x.ReceiverId == callerId)
                         && x.Status == RelationshipType.Accepted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new FriendDTO
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    Friend = x.SenderId == callerId
                        ? new UserDTO { Id = x.Receiver.Id, Username = x.Receiver.Username, ProfilePictureUrl = x.Receiver.ProfilePictureUrl }
                        : new UserDTO { Id = x.Sender.Id, Username = x.Sender.Username, ProfilePictureUrl = x.Sender.ProfilePictureUrl }
                }).Skip((currentPage-1)*pageSize).Take(pageSize)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message)> DeleteRequest(Guid relationshipId, Guid callerId) {
            try
            {
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    DELETE FROM ""Relationships""
                    WHERE Id = {relationshipId} 
                        AND (ReceiverId = {callerId} OR SenderId = {callerId})");

                if (rows > 0)
                {
                    return (true, "Successfully deleted request");
                }
                else
                {
                    return (false, "Request does not exist");
                }
            }
            catch (Exception e)
            {
                return (false, "An internal server error occurred while sending the request.");
            }
        }

        public async Task<(bool Success, string Message)> AcceptRequest(Guid relationshipId, Guid callerId)
        {
            try
            {
                int rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE ""Relationships""
                    SET ""Status"" = {(int)RelationshipType.Accepted} 
                        WHERE ""Id"" = {relationshipId} 
                          AND ""ReceiverId"" = {callerId} 
                          AND ""Status"" = {(int)RelationshipType.Waiting}");

                if (rows > 0)
                {
                    return (true, "Successfully accepted request");
                }
                else
                {
                    return (false, "Request does not exist");
                }
            }
            catch (Exception es)
            {
                // log error
                return (false, "An internal server error occurred while accepting the request.");
            }
        }
    }
}
