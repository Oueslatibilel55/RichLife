using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

/// <summary>The luxury shop (contract §6c). The rules — prestige, one of each, cash — are on Company.</summary>
public class LuxuryService(
    ICompanyRepository companyRepo,
    ILuxuryCatalogueRepository luxuryRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<LuxuryItemDto>>> GetShopAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<IReadOnlyList<LuxuryItemDto>>("Company not found.");

        var items = await luxuryRepo.GetActiveAsync(ct);
        var owned = company.LuxuryAssets.Select(l => l.CatalogueId).ToHashSet();

        return Result.Ok<IReadOnlyList<LuxuryItemDto>>(items.Select(i => new LuxuryItemDto(
            i.Id, i.Name, i.Category.ToString(), i.Description, i.Price, i.RequiredPrestige.ToString(),
            i.ImageUrl, i.ImageCredit, i.ImageSourceUrl,
            IsUnlocked: company.PrestigeLevel >= i.RequiredPrestige,
            IsOwned: owned.Contains(i.Id),
            CanAfford: company.Cash >= i.Price)).ToList());
    }

    public async Task<Result<OwnedLuxuryDto>> BuyAsync(Guid playerId, string itemId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<OwnedLuxuryDto>("Company not found.");

        var item = await luxuryRepo.GetByIdAsync(itemId, ct);
        if (item is null) return Result.Fail<OwnedLuxuryDto>("Item not found.");

        var result = company.BuyLuxury(item, clock.GetUtcNow().UtcDateTime);
        if (!result.IsSuccess) return Result.Fail<OwnedLuxuryDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(ToDto(result.Value));
    }

    public static OwnedLuxuryDto ToDto(LuxuryAsset l) =>
        new(l.CatalogueId, l.Name, l.Category.ToString(), l.Cost, l.ImageUrl, l.ImageCredit, l.CreatedAt);
}
