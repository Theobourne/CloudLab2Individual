using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Contracts.Models;

namespace StudentsAPI.Data
{
    public class StudentsAPIContext : DbContext
    {
        public StudentsAPIContext (DbContextOptions<StudentsAPIContext> options)
            : base(options)
        {
        }

        public DbSet<Contracts.Models.Student> Student { get; set; } = default!;
        public DbSet<Contracts.Models.Enrollment> Enrollment { get; set; }
    }
}
