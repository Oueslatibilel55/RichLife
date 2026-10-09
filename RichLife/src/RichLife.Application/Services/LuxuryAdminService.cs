using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Common;

namespace RichLife.Application.Services;

/// <summary>
/// Edits the luxury list (contract §7d). An edit applies to later purchases only — a bought
/// item copied its name, price and photo. No delete: retire with <c>IsActive = false</c>.
/// </summary>
public class LuxuryAdminService(
    ILuxuryCatalogueRepository luxuryRepo,
    IAdminReadRepository reads,
    IUnitOfWork uow)
{
    private const string ItemNotFound = "Item not found.";

    public async Task<IReadOnlyList<AdminLuxuryItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var owners = await reads.GetLuxuryOwnersAsync(ct);
        return (await luxuryRepo.GetAllAsync(ct)).Select(i => ToDto(i, owners)).ToList();
    }

    public async Task<AdminLuxuryItemDto?> GetAsync(string id, CancellationToken ct = default)
    {
        if (await luxuryRepo.GetByIdAsync(id, ct) is not { } item) return null;
        return ToDto(item, await reads.GetLuxuryOwnersAsync(ct));
    }

    public async Task<Result<AdminLuxuryItemDto>> CreateAsync(CreateLuxuryItemRequest req, CancellationToken ct = default)
    {
        // Validated before the lookup, so a malformed id never reaches the database.
        var created = LuxuryCatalogueEntry.CreateNew(req.Id, new LuxuryItemDetails(
            req.Name, req.Category, req.Description, req.Price, req.RequiredPrestige,
            req.ImageUrl, req.ImageCredit, req.ImageSourceUrl, req.DisplayOrder));
        if (!created.IsSuccess) return Result.Fail<AdminLuxuryItemDto>(created.Error!);

        if (await luxuryRepo.GetForUpdateAsync(req.Id, ct) is not null)
            return Result.Fail<AdminLuxuryItemDto>("Item id already exists.");

        await luxuryRepo.AddAsync(created.Value, ct);
        await uow.CommitAsync(ct);
        return Result.Ok(ToDto(created.Value, new Dictionary<string, int>()));
    }

    public async Task<Result<AdminLuxuryItemDto>> UpdateAsync(
        string id, UpdateLuxuryItemRequest req, CancellationToken ct = default)
    {
        var item = await luxuryRepo.GetForUpdateAsync(id, ct);
        if (item is null) return Result.Fail<AdminLuxuryItemDto>(ItemNotFound);

        var result = item.Update(new LuxuryItemDetails(
            req.Name, req.Category, req.Description, req.Price, req.RequiredPrestige,
            req.ImageUrl, req.ImageCredit, req.ImageSourceUrl, req.DisplayOrder), req.IsActive);
        if (!result.IsSuccess) return Result.Fail<AdminLuxuryItemDto>(result.Error!);

        await uow.CommitAsync(ct);
        return Result.Ok(ToDto(item, await reads.GetLuxuryOwnersAsync(ct)));
    }

    private static AdminLuxuryItemDto ToDto(LuxuryCatalogueEntry i, IReadOnlyDictionary<string, int> owners) => new(
        i.Id, i.Name, i.Category.ToString(), i.Description, i.Price, i.RequiredPrestige.ToString(),
        i.ImageUrl, i.ImageCredit, i.ImageSourceUrl, i.DisplayOrder, i.IsActive,
        owners.GetValueOrDefault(i.Id));
}
