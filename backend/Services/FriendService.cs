using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
// Important INFO: ALL THE METHODS WILL BE REMADE TO CONSIDER AUTH TOKENS, for now this is only for demonstration and will be changed accordingly later, now we consider that we have user token
namespace backend.Services
{
    public class FriendService
    {
        private readonly AppDbContext _context;

        public FriendService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddUser(Guid user_id)
        {
            try
            {
                User UserToAdd = await _context.Users
                    .AsNoTracking()
                    .Where(x => x.Id == user_id).
                    FirstOrDefaultAsync();

                if (UserToAdd == null)
                {
                    return false;
                }
                // when auth added sender will be from token
                Relationship reletionship = new Relationship
                {
                    ReceiverId = user_id,
                    SenderId = user_id,
                    Status = RelationshipType Waiting,
                };

                _context.Relationships.Add(reletionship);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // log error here
                return false;
            }
        }
        //public async Task<bool> RejectRequest(Guid reletionship_id)
        //{
            
        //}

        public async Task<WaitingRelationshipsDTO> WaitingRelationshipsList(Guid CentralUserId)
        {
            List<RelationshipDTO> ReceivedDTO = await _context.Relationships
                .Where(x => x.ReceiverId == CentralUserId && x.Status == RelationshipType.Waiting)
                .Select(x => new RelationshipDTO
                {
                    Id = x.Id,
                    Status = x.Status,
                    Sender = new UserDTO { Id = x.Sender.Id, Username = x.Sender.Username, ProfilePictureUrl = x.Sender.ProfilePictureUrl },
                })
                .ToListAsync();

            List<RelationshipDTO> SentDTO = await _context.Relationships
                .Where(x => x.SenderId == CentralUserId && x.Status == RelationshipType.Waiting)
                .Select(x => new RelationshipDTO
                {
                    Id = x.Id,
                    Status = x.Status,
                    Receiver = new UserDTO { Id = x.Receiver.Id, Username = x.Receiver.Username, ProfilePictureUrl = x.Receiver.ProfilePictureUrl },
                })
                .ToListAsync();

            return new WaitingRelationshipsDTO
            {
                Sent = SentDTO,
                Received = ReceivedDTO,
            };
        }

        public async Task<Relationship> RetrieveReletionship(Guid reletionship_id)
        {
            Reletionship reletionship = await _context.Relationships.
                .AsNoTrackking()
                .Where(x => x.Id == reletionship_id)
                .FirstOrDefaultAsync();

            return reletionship;
        }
    }
}
