using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Application.Mapping;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

public class BusinessService(
    ICompanyRepository companyRepo,
    ICatalogueRepository catalogueRepo,
    IManagerNameRepository managerNameRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    private DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    // -- Catalogue ----------------------------------------------------------

    public async Task<Result<IReadOnlyList<BusinessCatalogueDto>>> GetCatalogueAsync(
        Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null)
            return Result.Fail<IReadOnlyList<BusinessCatalogueDto>>("Company not found.");

        var entries = await catalogueRepo.GetAllAsync(ct);

        var dtos = entries
            .Where(e => e.IsActive && e.RequiredPrestige <= company.PrestigeLevel)
            .Select(e => CatalogueMapper.ToPlayerDto(
                e,
                company.Cash,
                company.Businesses.FirstOrDefault(b => b.CatalogueId == e.Id)))
            .ToList();

        return Result.Ok<IReadOnlyList<BusinessCatalogueDto>>(dtos);
    }

    // -- Open ---------------------------------------------------------------

    public async Task<Result<BusinessDto>> OpenBusinessAsync(
        Guid playerId, string catalogueId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        var entry = await catalogueRepo.GetByIdAsync(catalogueId, ct);
        if (entry is null) return Result.Fail<BusinessDto>("Business not found in catalogue.");

        var result = company.OpenBusiness(entry);
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, UtcNow));
    }

    // -- Close --------------------------------------------------------------

    public async Task<Result> CloseBusinessAsync(
        Guid playerId, Guid businessId, bool emergency, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail("Company not found.");

        var result = company.CloseBusiness(businessId, emergency);
        if (!result.IsSuccess) return result;

        companyRepo.Update(company);
        await uow.CommitAsync(ct);
        return Result.Ok();
    }

    // -- Hire a manager -----------------------------------------------------

    public async Task<Result<BusinessDto>> AutomateBusinessAsync(
        Guid playerId, Guid businessId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        // Running shifts get different names while unused ones remain.
        var now = UtcNow;
        var namesInUse = company.Businesses
            .Where(b => b.ManagerNameId is not null && b.HasManagerAt(now))
            .Select(b => b.ManagerNameId!.Value)
            .ToList();
        var manager = await managerNameRepo.PickRandomAsync(namesInUse, ct);
        if (manager is null) return Result.Fail<BusinessDto>("No managers are available right now.");

        var result = company.AutomateBusiness(businessId, manager, now);
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, UtcNow));
    }

    // -- Level up -----------------------------------------------------------

    public async Task<Result<BusinessDto>> LevelUpBusinessAsync(
        Guid playerId, Guid businessId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        var result = company.LevelUpBusiness(businessId);
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, UtcNow));
    }

    // -- Buy asset ----------------------------------------------------------

    public async Task<Result<BusinessDto>> BuyAssetAsync(
        Guid playerId, Guid businessId, string assetCatalogueId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        // Looked up here only to learn which catalogue entry to load; the aggregate
        // re-checks ownership itself.
        var business = company.Businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<BusinessDto>("Business not found.");

        var entry = await catalogueRepo.GetByIdAsync(business.CatalogueId, ct);
        if (entry is null) return Result.Fail<BusinessDto>("Business not found in catalogue.");

        var result = company.BuyAsset(businessId, entry, assetCatalogueId);
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, UtcNow));
    }
}
