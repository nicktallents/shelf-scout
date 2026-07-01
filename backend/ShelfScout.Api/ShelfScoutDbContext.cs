using Microsoft.EntityFrameworkCore;

namespace ShelfScout.Api;

public class ShelfScoutDbContext(DbContextOptions<ShelfScoutDbContext> options) : DbContext(options);
