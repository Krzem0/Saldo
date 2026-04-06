using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class MemberRepository : IMemberRepository
{
    private readonly SaldoDbContext _context;

    public MemberRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Member?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Members
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Member member, CancellationToken ct = default)
    {
        _context.Members.Add(member);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Member member, CancellationToken ct = default)
    {
        _context.Members.Update(member);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Members
            .Where(m => m.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
