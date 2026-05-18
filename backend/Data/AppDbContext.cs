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
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<ChannelMessage> ChannelMessages { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<ServerChannel> ServerChannels { get; set; }
        public DbSet<ServerParticipant> ServerParticipants { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Token> Tokens { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
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
        }
    }
}