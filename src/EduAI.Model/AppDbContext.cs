using EduAI.Model.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EduAI.Model;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<SubjectAssignment> SubjectAssignments => Set<SubjectAssignment>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentEmbedding> DocumentEmbeddings => Set<DocumentEmbedding>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IndexingSettings> IndexingSettings => Set<IndexingSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Subject>(entity =>
        {
            entity.HasIndex(s => s.Name).IsUnique();
            entity.HasOne(s => s.Teacher)
                .WithMany()
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SubjectAssignment>(entity =>
        {
            entity.HasIndex(a => new { a.SubjectId, a.Status });
            entity.HasOne(a => a.Subject)
                .WithMany(s => s.Assignments)
                .HasForeignKey(a => a.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.Teacher)
                .WithMany()
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Chapter>(entity =>
        {
            entity.HasOne(c => c.Subject)
                .WithMany(s => s.Chapters)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Lesson>(entity =>
        {
            entity.Property(l => l.Name).HasMaxLength(200);
            entity.HasOne(l => l.Chapter)
                .WithMany(c => c.Lessons)
                .HasForeignKey(l => l.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(l => new { l.ChapterId, l.Name }).IsUnique();
        });

        builder.Entity<Document>(entity =>
        {
            entity.Property(d => d.FileName).HasMaxLength(255);
            entity.Property(d => d.IndexError).HasMaxLength(1000);
            // A lesson can hold many documents, but file names must be unique within a lesson.
            entity.HasIndex(d => new { d.LessonId, d.FileName }).IsUnique();
            entity.HasOne(d => d.Subject)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            // Chapter is kept denormalized for queries. Cascade flows Chapter -> Lesson -> Document,
            // so the direct Chapter -> Document link must be Restrict (single cascade path only).
            entity.HasOne(d => d.Chapter)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);
            // Deleting a lesson (or its parent chapter) cascades to the lesson's documents.
            entity.HasOne(d => d.Lesson)
                .WithMany(l => l.Documents)
                .HasForeignKey(d => d.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.UploadedBy)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(d => d.LastModifiedBy)
                .WithMany()
                .HasForeignKey(d => d.LastModifiedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DocumentChunk>(entity =>
        {
            entity.HasOne(c => c.Subject)
                .WithMany()
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Chapter)
                .WithMany()
                .HasForeignKey(c => c.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DocumentEmbedding>(entity =>
        {
            entity.HasIndex(e => e.ChunkId).IsUnique();
            entity.HasOne(e => e.Chunk)
                .WithOne(c => c.Embedding)
                .HasForeignKey<DocumentEmbedding>(e => e.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatSession>(entity =>
        {
            entity.HasOne(s => s.Student)
                .WithMany()
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.Subject)
                .WithMany()
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne(m => m.ChatSession)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<IndexingSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ChunkSize).IsRequired();
            entity.Property(s => s.ChunkOverlap).IsRequired();
            entity.HasOne(s => s.UpdatedBy)
                .WithMany()
                .HasForeignKey(s => s.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

// Single-file grouping: DbContext + DI registration + EF design-time factory
public static class AppDbContextExtensions
{
    public static IServiceCollection AddAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }

    public static DbContextOptions<AppDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var webPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "EduAI.Web");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(webPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chưa cấu hình chuỗi kết nối 'DefaultConnection'.");

        return new AppDbContext(AppDbContextExtensions.CreateOptions(connectionString));
    }
}
