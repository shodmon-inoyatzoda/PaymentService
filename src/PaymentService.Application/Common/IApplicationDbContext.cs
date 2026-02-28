using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Entities.Users;

namespace PaymentService.Application.Common;

public interface IApplicationDbContext
{
    // Identity
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    
    // Order
    DbSet<Order> Orders { get; }
    
    // Payment
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}