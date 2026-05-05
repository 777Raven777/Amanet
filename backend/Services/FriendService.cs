using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
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
    }
}
