using Microsoft.EntityFrameworkCore;
using SabakiumAPI.Models;

namespace SabakiumAPI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();

        b.Entity<Post>()
            .HasOne(p => p.User)
            .WithMany(u => u.Posts)
            .HasForeignKey(p => p.UserId);

        b.Entity<PostLike>()
            .HasKey(l => new { l.PostId, l.UserId });

        b.Entity<PostLike>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId);

        b.Entity<PostLike>()
            .HasOne(l => l.User)
            .WithMany(u => u.PostLikes)
            .HasForeignKey(l => l.UserId);

        b.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<ChatMessage>()
            .HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Comment>().HasOne(c => c.Post).WithMany(p => p.Comments).HasForeignKey(c => c.PostId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Comment>().HasOne(c => c.User).WithMany(u => u.Comments).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}