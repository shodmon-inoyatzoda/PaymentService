using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features.Payments.Services;

namespace PaymentService.Infrastructure.Persistence;

internal sealed class PostgresOrderLockService : IOrderLockService
{
    private readonly ApplicationDbContext _db;

    public PostgresOrderLockService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task AcquireLockAsync(Guid orderId, CancellationToken cancellationToken) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM orders WHERE id = {orderId} FOR UPDATE",
            cancellationToken);
}
