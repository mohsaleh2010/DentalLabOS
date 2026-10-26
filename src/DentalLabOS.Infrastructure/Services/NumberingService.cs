using DentalLabOS.Application.Abstractions;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Infrastructure.Services;

/// <summary>
/// Allocates document numbers with an UPDLOCK'ed UPDATE ... OUTPUT so concurrent requests never get the same number.
/// Running inside the caller's transaction keeps the sequence gap-free when the document save rolls back (rule A2.1-3).
/// </summary>
public sealed class NumberingService(AppDbContext db, ITenantContext tenant) : INumberingService
{
    private sealed record Allocated(int Value, string Prefix, int Padding, bool ResetYearly, int Year);

    private const string AllocateSql = """
        DECLARE @out TABLE ([Value] int, [Prefix] nvarchar(10), [Padding] int, [ResetYearly] bit, [Year] int);
        UPDATE [DocumentSequences] WITH (UPDLOCK, ROWLOCK)
        SET [NextNumber] = [NextNumber] + 1
        OUTPUT deleted.[NextNumber], deleted.[Prefix], deleted.[Padding], deleted.[ResetYearly], deleted.[Year] INTO @out
        WHERE [TenantId] = @tenant AND [DocumentType] = @type
          AND ((@branch IS NULL AND [BranchId] IS NULL) OR [BranchId] = @branch)
          AND ([ResetYearly] = 0 OR [Year] = @year);
        SELECT [Value], [Prefix], [Padding], [ResetYearly], [Year] FROM @out;
        """;

    public async Task<string> NextAsync(string documentType, Guid? branchId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("Numbering requires a tenant context.");

        // Branch-specific sequence first, then the lab-wide one.
        foreach (var branch in branchId is null ? new Guid?[] { null } : [branchId, null])
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var row = await TryAllocateAsync(tenantId, documentType, branch, date.Year, cancellationToken);
                if (row is not null)
                {
                    var seq = new DocumentSequence { Prefix = row.Prefix, Padding = row.Padding, ResetYearly = row.ResetYearly, Year = row.Year };
                    return seq.Format(row.Value);
                }

                if (!await TryRollYearAsync(documentType, branch, date.Year, cancellationToken))
                {
                    break;
                }
            }
        }

        throw new InvalidOperationException($"No numbering sequence is configured for '{documentType}'.");
    }

    private async Task<Allocated?> TryAllocateAsync(Guid tenantId, string type, Guid? branch, int year, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<Allocated>(AllocateSql,
                new SqlParameter("@tenant", tenantId),
                new SqlParameter("@type", type),
                new SqlParameter("@branch", (object?)branch ?? DBNull.Value) { SqlDbType = System.Data.SqlDbType.UniqueIdentifier },
                new SqlParameter("@year", year))
            .ToListAsync(ct);
        return rows.FirstOrDefault();
    }

    /// <summary>Creates this year's row for a yearly-reset sequence by copying the most recent year's format.</summary>
    private async Task<bool> TryRollYearAsync(string type, Guid? branch, int year, CancellationToken ct)
    {
        var template = await db.DocumentSequences.AsNoTracking()
            .Where(s => s.DocumentType == type && s.BranchId == branch && s.ResetYearly && s.Year < year)
            .OrderByDescending(s => s.Year)
            .FirstOrDefaultAsync(ct);
        if (template is null)
        {
            return false;
        }

        var created = new DocumentSequence
        {
            DocumentType = type, BranchId = branch, Year = year, Prefix = template.Prefix, Padding = template.Padding, ResetYearly = true,
        };
        db.DocumentSequences.Add(created);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request created it first; the unique index guarantees a single row. Retry allocation.
            db.Entry(created).State = EntityState.Detached;
        }

        return true;
    }
}
