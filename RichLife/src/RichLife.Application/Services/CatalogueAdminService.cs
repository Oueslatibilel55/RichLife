using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Application.Mapping;
using RichLife.Domain.Catalogue;
using RichLife.Domain.Common;

namespace RichLife.Application.Services;

/// <summary>
/// Edits game content. Every change applies to businesses opened <i>after</i> it — an owned
/// business keeps the numbers it was opened with.
/// </summary>
public class CatalogueAdminService(ICatalogueRepository catalogueRepo, IUnitOfWork uow)
{
    private const string EntryNotFound = "Catalogue entry not found.";

    public async Task<IReadOnlyList<AdminCatalogueEntryDto>> GetAllAsync(CancellationToken ct = default)
        => (await catalogueRepo.GetAllAsync(ct)).Select(CatalogueMapper.ToAdminDto).ToList();

    public async Task<AdminCatalogueEntryDto?> GetAsync(string id, CancellationToken ct = default)
        => await catalogueRepo.GetByIdAsync(id, ct) is { } entry ? CatalogueMapper.ToAdminDto(entry) : null;

    public async Task<Result<AdminCatalogueEntryDto>> CreateAsync(
        CreateCatalogueEntryRequest req, CancellationToken ct = default)
    {
        // Validated before the lookup, so a malformed id never reaches the database.
        var created = BusinessCatalogueEntry.Create(req.Id, CatalogueMapper.ToDetails(req));
        if (!created.IsSuccess) return Result.Fail<AdminCatalogueEntryDto>(created.Error!);

        if (await catalogueRepo.GetForUpdateAsync(req.Id, ct) is not null)
            return Result.Fail<AdminCatalogueEntryDto>("Catalogue id already exists.");

        var entry = created.Value;
        foreach (var asset in req.AvailableAssets ?? [])
        {
            var added = entry.AddAsset(asset.Id, CatalogueMapper.ToDetails(asset));
            if (!added.IsSuccess) return Result.Fail<AdminCatalogueEntryDto>(added.Error!);
        }

        await catalogueRepo.AddAsync(entry, ct);
        await uow.CommitAsync(ct);
        return Result.Ok(CatalogueMapper.ToAdminDto(entry));
    }

    public Task<Result<AdminCatalogueEntryDto>> UpdateAsync(
        string id, UpdateCatalogueEntryRequest req, CancellationToken ct = default)
        => EditAsync(id, e => e.Update(CatalogueMapper.ToDetails(req), req.IsActive), ct);

    public Task<Result<AdminCatalogueEntryDto>> AddAssetAsync(
        string id, CreateCatalogueAssetRequest req, CancellationToken ct = default)
        => EditAsync(id, e => e.AddAsset(req.Id, CatalogueMapper.ToDetails(req)), ct);

    public Task<Result<AdminCatalogueEntryDto>> UpdateAssetAsync(
        string id, string assetId, UpdateCatalogueAssetRequest req, CancellationToken ct = default)
        => EditAsync(id, e => e.UpdateAsset(assetId, CatalogueMapper.ToDetails(req)), ct);

    public Task<Result<AdminCatalogueEntryDto>> RemoveAssetAsync(
        string id, string assetId, CancellationToken ct = default)
        => EditAsync(id, e => e.RemoveAsset(assetId), ct);

    /// <summary>Load tracked, apply one aggregate method, commit only if it succeeded.</summary>
    private async Task<Result<AdminCatalogueEntryDto>> EditAsync(
        string id, Func<BusinessCatalogueEntry, Result> edit, CancellationToken ct)
    {
        var entry = await catalogueRepo.GetForUpdateAsync(id, ct);
        if (entry is null) return Result.Fail<AdminCatalogueEntryDto>(EntryNotFound);

        var result = edit(entry);
        if (!result.IsSuccess) return Result.Fail<AdminCatalogueEntryDto>(result.Error!);

        await uow.CommitAsync(ct);
        return Result.Ok(CatalogueMapper.ToAdminDto(entry));
    }
}
