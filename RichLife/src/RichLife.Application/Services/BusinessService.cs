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

    // -- Taxes (features/013-business-taxes.md) ----------------------------

    public Task<Result<TaxPaymentDto>> PayTaxesAsync(Guid playerId, Guid businessId, CancellationToken ct = default) =>
        PayAsync(playerId, c => c.PayBusinessTaxes(businessId), ct);

    public Task<Result<TaxPaymentDto>> PayAllTaxesAsync(Guid playerId, CancellationToken ct = default) =>
        PayAsync(playerId, c => c.PayAllTaxes(), ct);

    private async Task<Result<TaxPaymentDto>> PayAsync(
        Guid playerId, Func<Company, Result<decimal>> pay, CancellationToken ct)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<TaxPaymentDto>("Company not found.");

        // A period that ended since the last sync is billed first, so the payment covers it.
        var now = UtcNow;
        company.AssessTaxes(now);
        var result = pay(company);
        if (!result.IsSuccess) return Result.Fail<TaxPaymentDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(new TaxPaymentDto(result.Value, company.Cash, CompanyMapper.ToDto(company, now)));
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

        var now = UtcNow;
        var result = company.BuyAsset(businessId, entry, assetCatalogueId, now);
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, now));
    }

    // -- Places, units and repairs (features/014-business-places-and-equipment.md) --

    public async Task<Result<BusinessDto>> OpenPlaceAsync(
        Guid playerId, Guid businessId, string placeKindId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        var business = company.Businesses.FirstOrDefault(b => b.Id == businessId);
        if (business is null) return Result.Fail<BusinessDto>("Business not found.");

        var entry = await catalogueRepo.GetByIdAsync(business.CatalogueId, ct);
        if (entry is null) return Result.Fail<BusinessDto>("Business not found in catalogue.");

        return await CommitAsync(company, company.OpenPlace(businessId, entry, placeKindId), ct);
    }

    public async Task<Result<BusinessDto>> LevelUpPlaceAsync(
        Guid playerId, Guid placeId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        return await CommitAsync(company, company.LevelUpPlace(placeId), ct);
    }

    public async Task<Result<BusinessDto>> BuyUnitAsync(
        Guid playerId, Guid placeId, string assetCatalogueId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        // Looked up here only to learn which catalogue entry to load.
        var business = company.Businesses.FirstOrDefault(b => b.Places.Any(p => p.Id == placeId));
        if (business is null) return Result.Fail<BusinessDto>("Place not found.");

        var entry = await catalogueRepo.GetByIdAsync(business.CatalogueId, ct);
        if (entry is null) return Result.Fail<BusinessDto>("Business not found in catalogue.");

        return await CommitAsync(company, company.BuyUnit(placeId, entry, assetCatalogueId, UtcNow), ct);
    }

    public async Task<Result<BusinessDto>> RepairUnitAsync(
        Guid playerId, Guid unitId, bool fast, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        return await CommitAsync(company, company.RepairUnit(unitId, fast, UtcNow), ct);
    }

    public async Task<Result<BusinessDto>> RepairAllAsync(
        Guid playerId, Guid businessId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<BusinessDto>("Company not found.");

        return await CommitAsync(company, company.RepairAllUnits(businessId, UtcNow), ct);
    }

    private async Task<Result<BusinessDto>> CommitAsync(Company company, Result<Business> result, CancellationToken ct)
    {
        if (!result.IsSuccess) return Result.Fail<BusinessDto>(result.Error!);

        companyRepo.Update(company);
        await uow.CommitAsync(ct);

        return Result.Ok(CompanyMapper.ToDto(result.Value, UtcNow));
    }
}
