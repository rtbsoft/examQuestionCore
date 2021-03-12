using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExamQuestion.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<School> Schools { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //change the default 'cascade delete' behavior for foreign keys
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
                relationship.DeleteBehavior = DeleteBehavior.Restrict;

            //make sure all decimals have two decimal places
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal)))
                property.SetColumnType("decimal(18,2)");

            //store the string version of the enum in the database - for readability
            modelBuilder.Entity<Course>().Property(e => e.Semester)
                .HasConversion(new EnumToStringConverter<Semesters>());

            //don't allow the same student number to be used more than once in a course
            modelBuilder.Entity<Student>().HasIndex(s => new {s.CourseId, s.Number}).IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}