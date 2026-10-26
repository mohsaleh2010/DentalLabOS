using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;
using DentalLabOS.Contracts.Admin;
using DentalLabOS.Domain.Organization;
using DentalLabOS.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace DentalLabOS.Application.Admin;

/// <summary>ADM-11. Only format settings are editable; the counter itself only moves forward via allocation.</summary>
public sealed class NumberingAdminService(IApplicationDbContext db, IPermissionService permissions)
{
    public async Task<IReadOnlyList<DocumentSequenceDto>> ListAsync(CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.NumberingManage, ct);
        var rows = await db.DocumentSequences.AsNoTracking()
            .OrderBy(s => s.DocumentType).ThenByDescending(s => s.Year)
            .Select(s => new { Seq = s, Branch = db.Branches.Where(b => b.Id == s.BranchId).Select(b => b.Name).FirstOrDefault() })
            .ToListAsync(ct);
        return rows.Select(r => new DocumentSequenceDto(r.Seq.Id, r.Seq.DocumentType, r.Branch, r.Seq.Year, r.Seq.Prefix, r.Seq.NextNumber,
            r.Seq.Padding, r.Seq.ResetYearly, r.Seq.Format(r.Seq.NextNumber), RowVersions.Encode(r.Seq.RowVersion))).ToList();
    }

    public async Task UpdateAsync(Guid id, UpdateSequenceRequest request, CancellationToken ct = default)
    {
        await permissions.DemandAsync(Permissions.Admin.NumberingManage, ct);
        var seq = await db.DocumentSequences.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(DocumentSequence));
        new Validator()
            .Required(nameof(request.Prefix), request.Prefix)
            .Pattern(nameof(request.Prefix), request.Prefix, "^[A-Za-z0-9]{1,10}$", "Validation.Code")
            .Range(nameof(request.Padding), request.Padding, 3, 10)
            .ThrowIfInvalid();

        db.SetOriginalRowVersion(seq, request.RowVersion);
        seq.Prefix = request.Prefix.Trim().ToUpperInvariant();
        seq.Padding = request.Padding;
        seq.ResetYearly = request.ResetYearly;
        await db.SaveChangesAsync(ct);
    }
}
