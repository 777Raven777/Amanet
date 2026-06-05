using backend.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Relationship> Relationships { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        //public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<ChannelMessage> ChannelMessages { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<ServerChannel> ServerChannels { get; set; }
        public DbSet<ServerParticipant> ServerParticipants { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Token> Tokens { get; set; }
        public DbSet<ServerInvite> ServerInvites { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var type in modelBuilder.Model.GetEntityTypes())
            {
                modelBuilder.Entity(type.ClrType)
                    .Property(nameof(BaseEntity.Id))
                    .HasValueGenerator<GuidV7ValueGenerator>()
                    .ValueGeneratedOnAdd();
            }

            modelBuilder.Entity<Relationship>(entity =>
            {
                entity.HasOne(r => r.Sender)
                      .WithMany()
                      .HasForeignKey(r => r.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Receiver)
                      .WithMany()
                      .HasForeignKey(r => r.ReceiverId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasOne(c => c.UserLow)
                      .WithMany()
                      .HasForeignKey(c => c.UserLowId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.UserHigh)
                      .WithMany()
                      .HasForeignKey(c => c.UserHighId)
                      .OnDelete(DeleteBehavior.Restrict);

                // One conversation per pair of users.
                entity.HasIndex(c => new { c.UserLowId, c.UserHighId }).IsUnique();

                // Enforce the "lower ID on the left"
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Conversation_UserOrder",
                    "\"UserLowId\" < \"UserHighId\""));
            });

            modelBuilder.Entity<ServerInvite>(entity =>
            {
                entity.HasOne(i => i.Server)
                      .WithMany()
                      .HasForeignKey(i => i.ServerId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(i => i.Inviter)
                      .WithMany()
                      .HasForeignKey(i => i.InviterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.InvitedUser)
                      .WithMany()
                      .HasForeignKey(i => i.InvitedUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}