#!/usr/bin/env pwsh
# ============================================================
#  New-RichLife.ps1
#  Scaffolds the full Rich Life backend solution
#  Requirements: .NET 10 SDK, Docker Desktop
#  Usage: ./New-RichLife.ps1 [-Root "C:\Projects"]
# ============================================================
param(
    [string]$Root = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -- Colors -------------------------------------------------------------------
function Info  ($m) { Write-Host "  $m" -ForegroundColor Cyan }
function Ok    ($m) { Write-Host "  [OK] $m" -ForegroundColor Green }
function Title ($m) { Write-Host "`n>> $m" -ForegroundColor Yellow }

# -- Helper: write file (creates parent dirs automatically) -------------------
function Write-File ([string]$Path, [string]$Content) {
    $dir = Split-Path $Path -Parent
    if ($dir -and !(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Set-Content -Path $Path -Value $Content -Encoding UTF8
}

# -- Resolve root -------------------------------------------------------------
$SolutionRoot = Resolve-Path $Root
$SolutionName = "RichLife"
$SolDir = Join-Path $SolutionRoot $SolutionName

if (Test-Path $SolDir) {
    Write-Host "`n  Folder '$SolDir' already exists. Delete it? (y/N): " -NoNewline -ForegroundColor Red
    if ((Read-Host) -ne 'y') { exit 0 }
    Remove-Item $SolDir -Recurse -Force
}

New-Item -ItemType Directory -Path $SolDir | Out-Null
Set-Location $SolDir

Title "Creating solution & projects"

# -- Solution ------------------------------------------------------------------
dotnet new sln -n $SolutionName | Out-Null
Ok "Solution created"

$projects = @(
    @{ Name="RichLife.Domain";          Template="classlib"; Src=$true  },
    @{ Name="RichLife.Application";     Template="classlib"; Src=$true  },
    @{ Name="RichLife.Infrastructure";  Template="classlib"; Src=$true  },
    @{ Name="RichLife.Api";             Template="webapi";   Src=$true  },
    @{ Name="RichLife.AppHost";         Template="classlib"; Src=$true  },
    @{ Name="RichLife.ServiceDefaults"; Template="classlib"; Src=$true  }
)

foreach ($p in $projects) {
    $dir = if ($p.Src) { Join-Path "src" $p.Name } else { $p.Name }
    dotnet new $p.Template -n $p.Name -o $dir --framework net10.0 | Out-Null
    dotnet sln add $dir | Out-Null
    # Remove default boilerplate
    Get-ChildItem $dir -Filter "*.cs" | Remove-Item -Force
    Ok $p.Name
}

# -- Project references --------------------------------------------------------
Title "Adding project references"

$refs = @(
    @{ From="src/RichLife.Application";    To="src/RichLife.Domain"          },
    @{ From="src/RichLife.Infrastructure"; To="src/RichLife.Application"     },
    @{ From="src/RichLife.Api";            To="src/RichLife.Application"      },
    @{ From="src/RichLife.Api";            To="src/RichLife.Infrastructure"   },
    @{ From="src/RichLife.Api";            To="src/RichLife.ServiceDefaults"  },
    @{ From="src/RichLife.AppHost";        To="src/RichLife.Api"              },
    @{ From="src/RichLife.AppHost";        To="src/RichLife.ServiceDefaults"  }
)
foreach ($r in $refs) {
    dotnet add $r.From reference $r.To | Out-Null
}
Ok "References linked"

# -- NuGet packages -------------------------------------------------------------
Title "Installing NuGet packages"

function Add-Pkg ($proj, $pkg) {
    Info "$proj  _  $pkg"
    dotnet add "src/$proj" package $pkg | Out-Null
}

# Infrastructure
Add-Pkg "RichLife.Infrastructure" "Microsoft.EntityFrameworkCore"
Add-Pkg "RichLife.Infrastructure" "Microsoft.EntityFrameworkCore.Tools"
Add-Pkg "RichLife.Infrastructure" "Npgsql.EntityFrameworkCore.PostgreSQL"
Add-Pkg "RichLife.Infrastructure" "Microsoft.Extensions.DependencyInjection.Abstractions"
Add-Pkg "RichLife.Infrastructure" "Microsoft.Extensions.Caching.Memory"

# API
Add-Pkg "RichLife.Api" "Microsoft.AspNetCore.Authentication.JwtBearer"
Add-Pkg "RichLife.Api" "Microsoft.AspNetCore.OpenApi"
Add-Pkg "RichLife.Api" "Scalar.AspNetCore"
Add-Pkg "RichLife.Api" "Microsoft.EntityFrameworkCore.Design"
Add-Pkg "RichLife.Api" "BCrypt.Net-Next"

# AppHost (Aspire)
Add-Pkg "RichLife.AppHost" "Aspire.Hosting"
Add-Pkg "RichLife.AppHost" "Aspire.Hosting.PostgreSQL"

# ServiceDefaults
Add-Pkg "RichLife.ServiceDefaults" "Microsoft.Extensions.Http.Resilience"
Add-Pkg "RichLife.ServiceDefaults" "Microsoft.Extensions.ServiceDiscovery"
Add-Pkg "RichLife.ServiceDefaults" "OpenTelemetry.Exporter.OpenTelemetryProtocol"
Add-Pkg "RichLife.ServiceDefaults" "OpenTelemetry.Extensions.Hosting"
Add-Pkg "RichLife.ServiceDefaults" "OpenTelemetry.Instrumentation.AspNetCore"
Add-Pkg "RichLife.ServiceDefaults" "OpenTelemetry.Instrumentation.Http"
Add-Pkg "RichLife.ServiceDefaults" "OpenTelemetry.Instrumentation.Runtime"

Ok "Packages installed"

# ================================================================================
# SOURCE FILES
# ================================================================================
Title "Generating source files"

# -- DOMAIN -------------------------------------------------------------------
Write-File "src/RichLife.Domain/Common/IDomainEvent.cs" @'
namespace RichLife.Domain.Common;

public interface IDomainEvent { }
'@

Write-File "src/RichLife.Domain/Common/AggregateRoot.cs" @'
namespace RichLife.Domain.Common;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent @event) => _domainEvents.Add(@event);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
'@

Write-File "src/RichLife.Domain/Common/BaseEntity.cs" @'
namespace RichLife.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    protected void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
}
'@

Write-File "src/RichLife.Domain/Enums/PrestigeLevel.cs" @'
namespace RichLife.Domain.Enums;

public enum PrestigeLevel
{
    TheHustle      = 1,
    SmallBusiness  = 2,
    Entrepreneur   = 3,
    BusinessMogul  = 4,
    Tycoon         = 5,
    Billionaire    = 6,
    GlobalEmpire   = 7
}
'@

Write-File "src/RichLife.Domain/Enums/BusinessSector.cs" @'
namespace RichLife.Domain.Enums;

public enum BusinessSector
{
    Transport    = 1,
    RealEstate   = 2,
    StockMarket  = 3,
    TechStartup  = 4,
    Hospitality  = 5,
    Energy       = 6
}
'@

Write-File "src/RichLife.Domain/Entities/Player.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

public class Player : AggregateRoot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Navigation
    public Company? Company { get; private set; }

    private Player() { }

    public static Player Create(string username, string email, string passwordHash, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new Player
        {
            Username     = username,
            Email        = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Country      = country
        };
    }
}
'@

Write-File "src/RichLife.Domain/Entities/Company.cs" @'
using RichLife.Domain.Common;
using RichLife.Domain.Enums;
using RichLife.Domain.Events;

namespace RichLife.Domain.Entities;

public class Company : AggregateRoot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlayerId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    // Economy
    public decimal Cash { get; private set; }
    public decimal PassiveIncomePerSecond { get; private set; } = 3m;
    public decimal AllTimeEarnings { get; private set; }
    public decimal NetWorth => Cash + Assets.Sum(a => a.CurrentValue);

    // Prestige
    public PrestigeLevel PrestigeLevel { get; private set; } = PrestigeLevel.TheHustle;
    public int PrestigeCount { get; private set; }
    public decimal PrestigeMultiplier => 1m + (0.18m * PrestigeCount);

    // Sync
    public DateTime LastSyncAt { get; private set; } = DateTime.UtcNow;

    // Collections
    private readonly List<Business> _businesses = [];
    private readonly List<Asset> _assets = [];
    private readonly List<LuxuryAsset> _luxuryAssets = [];

    public IReadOnlyList<Business> Businesses => _businesses.AsReadOnly();
    public IReadOnlyList<Asset> Assets => _assets.AsReadOnly();
    public IReadOnlyList<LuxuryAsset> LuxuryAssets => _luxuryAssets.AsReadOnly();

    private Company() { }

    public static Company Create(Guid playerId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Company { PlayerId = playerId, Name = name };
    }

    // -- Economy ------------------------------------------------------------

    public void ApplyOfflineProgress(TimeSpan elapsed)
    {
        var cap = TimeSpan.FromHours(4);
        var effective = elapsed > cap ? cap : elapsed;
        var seconds = (decimal)effective.TotalSeconds;

        var automatedIncome = _businesses
            .Where(b => b.IsAutomated)
            .Sum(b => b.NetIncomePerSecond);

        var total = (PassiveIncomePerSecond + automatedIncome) * PrestigeMultiplier * seconds;
        AddCash(total);
        LastSyncAt = DateTime.UtcNow;
    }

    public void Tick(decimal seconds)
    {
        var income = (PassiveIncomePerSecond + _businesses.Sum(b => b.NetIncomePerSecond))
                     * PrestigeMultiplier * seconds;
        AddCash(income);
        LastSyncAt = DateTime.UtcNow;
    }

    public void AddCash(decimal amount)
    {
        if (amount <= 0) return;
        Cash += amount;
        AllTimeEarnings += amount;
        MarkUpdated();
    }

    public Result DeductCash(decimal amount)
    {
        if (amount > Cash) return Result.Fail("Insufficient funds.");
        Cash -= amount;
        MarkUpdated();
        return Result.Ok();
    }

    // -- Business -----------------------------------------------------------

    public Result OpenBusiness(Business business)
    {
        var cost = business.OpeningCost;
        var deduct = DeductCash(cost);
        if (!deduct.IsSuccess) return deduct;

        _businesses.Add(business);
        RaiseDomainEvent(new BusinessOpenedEvent(Id, business.Id, business.Name));
        MarkUpdated();
        return Result.Ok();
    }

    public Result ListBusinessForSale(Guid businessId, decimal askingPrice)
    {
        var biz = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (biz is null) return Result.Fail("Business not found.");
        biz.ListForSale(askingPrice);
        return Result.Ok();
    }

    public Result CloseBusiness(Guid businessId, bool emergency = false)
    {
        var biz = _businesses.FirstOrDefault(b => b.Id == businessId);
        if (biz is null) return Result.Fail("Business not found.");

        var feeRate = emergency ? 0.25m : 0.10m;
        var refund = biz.OpeningCost * (1m - feeRate);
        AddCash(refund);
        _businesses.Remove(biz);
        MarkUpdated();
        return Result.Ok();
    }

    // -- Upgrade passive income ---------------------------------------------

    public Result UpgradePassiveIncome(decimal cost, decimal newRate)
    {
        var deduct = DeductCash(cost);
        if (!deduct.IsSuccess) return deduct;
        PassiveIncomePerSecond = newRate;
        MarkUpdated();
        return Result.Ok();
    }

    // -- Prestige -----------------------------------------------------------

    public Result CanPrestige()
    {
        if ((int)PrestigeLevel >= 7)
            return Result.Fail("Already at maximum prestige.");
        if (NetWorth < GetPrestigeThreshold())
            return Result.Fail($"Net worth must reach {GetPrestigeThreshold():C0} to prestige.");
        return Result.Ok();
    }

    public Result Prestige()
    {
        var canPrestige = CanPrestige();
        if (!canPrestige.IsSuccess) return canPrestige;

        PrestigeCount++;
        PrestigeLevel = (PrestigeLevel)((int)PrestigeLevel + 1);
        Cash = 0;
        _businesses.Clear();

        RaiseDomainEvent(new PrestigeTriggeredEvent(Id, PrestigeLevel, PrestigeMultiplier));
        MarkUpdated();
        return Result.Ok();
    }

    private decimal GetPrestigeThreshold() => PrestigeLevel switch
    {
        PrestigeLevel.TheHustle     => 25_000m,
        PrestigeLevel.SmallBusiness => 200_000m,
        PrestigeLevel.Entrepreneur  => 2_000_000m,
        PrestigeLevel.BusinessMogul => 20_000_000m,
        PrestigeLevel.Tycoon        => 250_000_000m,
        PrestigeLevel.Billionaire   => 3_000_000_000m,
        _ => decimal.MaxValue
    };

    private void MarkUpdated() { }
}
'@

Write-File "src/RichLife.Domain/Entities/Business.cs" @'
using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Entities;

public class Business : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public BusinessSector Sector { get; private set; }
    public PrestigeLevel RequiredPrestige { get; private set; }
    public decimal OpeningCost { get; private set; }
    public decimal GrossIncomePerSecond { get; private set; }
    public decimal MonthlySalaryCost { get; private set; }
    public bool IsAutomated { get; private set; }
    public bool IsForSale { get; private set; }
    public decimal? AskingPrice { get; private set; }
    public int EmployeeCount { get; private set; }

    // Assets owned by this business (trucks, cars, rooms, etc.)
    private readonly List<BusinessAsset> _assets = [];
    public IReadOnlyList<BusinessAsset> Assets => _assets.AsReadOnly();

    // salary deducted per second (monthly / 2_592_000)
    public decimal SalaryCostPerSecond => MonthlySalaryCost / 2_592_000m;
    public decimal NetIncomePerSecond => GrossIncomePerSecond - SalaryCostPerSecond;

    private Business() { }

    public static Business Create(
        Guid companyId, string name, BusinessSector sector,
        PrestigeLevel requiredPrestige, decimal openingCost,
        decimal grossIncomePerSecond, decimal monthlySalaryCost,
        int employeeCount)
    {
        return new Business
        {
            CompanyId            = companyId,
            Name                 = name,
            Sector               = sector,
            RequiredPrestige     = requiredPrestige,
            OpeningCost          = openingCost,
            GrossIncomePerSecond = grossIncomePerSecond,
            MonthlySalaryCost    = monthlySalaryCost,
            EmployeeCount        = employeeCount
        };
    }

    public void Automate() { IsAutomated = true; MarkUpdated(); }

    public void AddAsset(BusinessAsset asset)
    {
        _assets.Add(asset);
        // income rule: income/s = asset_value * 0.0005
        GrossIncomePerSecond += asset.PurchasePrice * 0.0005m;
        MarkUpdated();
    }

    public void ListForSale(decimal askingPrice)
    {
        IsForSale   = true;
        AskingPrice = askingPrice;
        MarkUpdated();
    }

    public void CancelSaleListing()
    {
        IsForSale   = false;
        AskingPrice = null;
        MarkUpdated();
    }
}
'@

Write-File "src/RichLife.Domain/Entities/BusinessAsset.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>
/// An asset owned by a Business (truck, car, hotel room, solar panel...).
/// income/s = PurchasePrice * 0.0005
/// </summary>
public class BusinessAsset : BaseEntity
{
    public Guid BusinessId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PurchasePrice { get; private set; }
    public decimal CurrentValue { get; private set; }

    private BusinessAsset() { }

    public static BusinessAsset Create(Guid businessId, string name, decimal purchasePrice)
        => new() { BusinessId = businessId, Name = name, PurchasePrice = purchasePrice, CurrentValue = purchasePrice };
}
'@

Write-File "src/RichLife.Domain/Entities/Asset.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

/// <summary>Luxury asset owned directly by the Company (jet, yacht, island).</summary>
public class Asset : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PurchasePrice { get; private set; }
    public decimal CurrentValue { get; private set; }
    public decimal PassiveIncomeBonus { get; private set; }

    private Asset() { }

    public static Asset Create(Guid companyId, string name, decimal price, decimal incomeBonus)
        => new() { CompanyId = companyId, Name = name, PurchasePrice = price, CurrentValue = price, PassiveIncomeBonus = incomeBonus };
}
'@

Write-File "src/RichLife.Domain/Entities/LuxuryAsset.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Domain.Entities;

public class LuxuryAsset : BaseEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Cost { get; private set; }
    public decimal IncomeMultiplierBonus { get; private set; }

    private LuxuryAsset() { }

    public static LuxuryAsset Create(Guid companyId, string name, decimal cost, decimal incomeBonus)
        => new() { CompanyId = companyId, Name = name, Cost = cost, IncomeMultiplierBonus = incomeBonus };
}
'@

Write-File "src/RichLife.Domain/Common/Result.cs" @'
namespace RichLife.Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    protected Result(bool success, string? error = null)
    {
        IsSuccess = success;
        Error     = error;
    }

    public static Result Ok()            => new(true);
    public static Result Fail(string e)  => new(false, e);

    public static Result<T> Ok<T>(T v)         => new(true, v);
    public static Result<T> Fail<T>(string e)  => new(false, default, e);
}

public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(bool success, T? value = default, string? error = null)
        : base(success, error) => Value = value;
}
'@

Write-File "src/RichLife.Domain/Events/BusinessOpenedEvent.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Domain.Events;

public record BusinessOpenedEvent(Guid CompanyId, Guid BusinessId, string BusinessName) : IDomainEvent;
'@

Write-File "src/RichLife.Domain/Events/PrestigeTriggeredEvent.cs" @'
using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Events;

public record PrestigeTriggeredEvent(Guid CompanyId, PrestigeLevel NewLevel, decimal Multiplier) : IDomainEvent;
'@

Write-File "src/RichLife.Domain/GameConstants.cs" @'
namespace RichLife.Domain;

public static class GameConstants
{
    // Passive income tiers: (cost, newRate)
    public static readonly (decimal Cost, decimal Rate)[] PassiveIncomeTiers =
    [
        (0m,           3m),
        (20_000m,      7m),
        (80_000m,      15m),
        (300_000m,     35m),
        (1_200_000m,   80m),
        (5_000_000m,   200m),
    ];

    // Asset income rule
    public const decimal AssetIncomeRatio = 0.0005m;
    public const decimal RentalIncomeRatio = 0.0004m;

    // Offline cap
    public static readonly TimeSpan OfflineCap = TimeSpan.FromHours(4);

    // Ad boost
    public const decimal AdIncomeMultiplier = 5m;
    public static readonly TimeSpan AdBoostDuration = TimeSpan.FromMinutes(30);
    public const int MaxAdBoostsPerDay = 10;

    // Marketplace closing fees
    public const decimal GracefulCloseFeeRate  = 0.10m;
    public const decimal EmergencyCloseFeeRate = 0.25m;
    public const decimal BankruptcyCloseFeeRate = 0.40m;
}
'@

Ok "Domain layer"

# -- APPLICATION ---------------------------------------------------------------
Write-File "src/RichLife.Application/Interfaces/ICompanyRepository.cs" @'
using RichLife.Domain.Entities;

namespace RichLife.Application.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByPlayerIdAsync(Guid playerId, CancellationToken ct = default);
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
    void Update(Company company);
}
'@

Write-File "src/RichLife.Application/Interfaces/IPlayerRepository.cs" @'
using RichLife.Domain.Entities;

namespace RichLife.Application.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Player?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task AddAsync(Player player, CancellationToken ct = default);
}
'@

Write-File "src/RichLife.Application/Interfaces/IUnitOfWork.cs" @'
namespace RichLife.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
'@

Write-File "src/RichLife.Application/Interfaces/IDomainEventDispatcher.cs" @'
using RichLife.Domain.Common;

namespace RichLife.Application.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default);
}
'@

Write-File "src/RichLife.Application/DTOs/CompanyDto.cs" @'
using RichLife.Domain.Enums;

namespace RichLife.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string Name,
    decimal Cash,
    decimal PassiveIncomePerSecond,
    decimal NetWorth,
    PrestigeLevel PrestigeLevel,
    int PrestigeCount,
    decimal PrestigeMultiplier,
    DateTime LastSyncAt,
    IReadOnlyList<BusinessDto> Businesses
);

public record BusinessDto(
    Guid Id,
    string Name,
    string Sector,
    decimal OpeningCost,
    decimal NetIncomePerSecond,
    bool IsAutomated,
    bool IsForSale,
    decimal? AskingPrice,
    int AssetCount
);

public record OfflineEarningsDto(
    TimeSpan Elapsed,
    decimal Earned,
    decimal CashBefore,
    decimal CashAfter
);
'@

Write-File "src/RichLife.Application/DTOs/AuthDto.cs" @'
namespace RichLife.Application.DTOs;

public record RegisterRequest(string Username, string Email, string Password, string Country);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId, string Username);
'@

Write-File "src/RichLife.Application/Services/CompanyService.cs" @'
using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;

namespace RichLife.Application.Services;

public class CompanyService(
    ICompanyRepository companyRepo,
    IUnitOfWork uow,
    IDomainEventDispatcher dispatcher)
{
    public async Task<Result<CompanyDto>> GetOrCreateAsync(Guid playerId, string companyName, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);

        if (company is null)
        {
            company = Company.Create(playerId, companyName);
            await companyRepo.AddAsync(company, ct);
            await uow.CommitAsync(ct);
        }

        return Result.Ok(MapToDto(company));
    }

    public async Task<Result<OfflineEarningsDto>> LoadWithOfflineProgressAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail<OfflineEarningsDto>("Company not found.");

        var elapsed = DateTime.UtcNow - company.LastSyncAt;
        var cashBefore = company.Cash;

        company.ApplyOfflineProgress(elapsed);

        companyRepo.Update(company);
        await dispatcher.DispatchAsync(company.DomainEvents, ct);
        company.ClearDomainEvents();
        await uow.CommitAsync(ct);

        return Result.Ok(new OfflineEarningsDto(elapsed, company.Cash - cashBefore, cashBefore, company.Cash));
    }

    public async Task<Result> PrestigeAsync(Guid playerId, CancellationToken ct = default)
    {
        var company = await companyRepo.GetByPlayerIdAsync(playerId, ct);
        if (company is null) return Result.Fail("Company not found.");

        var result = company.Prestige();
        if (!result.IsSuccess) return result;

        companyRepo.Update(company);
        await dispatcher.DispatchAsync(company.DomainEvents, ct);
        company.ClearDomainEvents();
        await uow.CommitAsync(ct);
        return Result.Ok();
    }

    private static BusinessDto MapBusinessToDto(Domain.Entities.Business b) => new(
        b.Id, b.Name, b.Sector.ToString(), b.OpeningCost,
        b.NetIncomePerSecond, b.IsAutomated, b.IsForSale, b.AskingPrice, b.Assets.Count);

    public static CompanyDto MapToDto(Company c) => new(
        c.Id, c.Name, c.Cash, c.PassiveIncomePerSecond, c.NetWorth,
        c.PrestigeLevel, c.PrestigeCount, c.PrestigeMultiplier, c.LastSyncAt,
        c.Businesses.Select(MapBusinessToDto).ToList());
}
'@

Write-File "src/RichLife.Application/Services/AuthService.cs" @'
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RichLife.Application.DTOs;
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;
using RichLife.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RichLife.Application.Services;

public class AuthService(IPlayerRepository playerRepo, IUnitOfWork uow, IConfiguration config)
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (await playerRepo.UsernameExistsAsync(req.Username, ct))
            return Result.Fail<AuthResponse>("Username already taken.");

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var player = Player.Create(req.Username, req.Email, hash, req.Country);

        await playerRepo.AddAsync(player, ct);
        await uow.CommitAsync(ct);

        return Result.Ok(GenerateTokens(player));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var player = await playerRepo.GetByEmailAsync(req.Email.ToLowerInvariant(), ct);
        if (player is null || !BCrypt.Net.BCrypt.Verify(req.Password, player.PasswordHash))
            return Result.Fail<AuthResponse>("Invalid credentials.");

        return Result.Ok(GenerateTokens(player));
    }

    private AuthResponse GenerateTokens(Player player)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Name,           player.Username),
            new Claim("country",                  player.Country)
        };

        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);

        var accessToken  = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return new AuthResponse(accessToken, refreshToken, player.Id, player.Username);
    }
}
'@

Write-File "src/RichLife.Application/Extensions/ApplicationExtensions.cs" @'
using Microsoft.Extensions.DependencyInjection;
using RichLife.Application.Services;

namespace RichLife.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CompanyService>();
        services.AddScoped<AuthService>();
        return services;
    }
}
'@

Ok "Application layer"

# -- INFRASTRUCTURE ------------------------------------------------------------
Write-File "src/RichLife.Infrastructure/Persistence/GameDbContext.cs" @'
using Microsoft.EntityFrameworkCore;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Player>        Players        => Set<Player>();
    public DbSet<Company>       Companies      => Set<Company>();
    public DbSet<Business>      Businesses     => Set<Business>();
    public DbSet<BusinessAsset> BusinessAssets => Set<BusinessAsset>();
    public DbSet<Asset>         Assets         => Set<Asset>();
    public DbSet<LuxuryAsset>   LuxuryAssets   => Set<LuxuryAsset>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}
'@

Write-File "src/RichLife.Infrastructure/Persistence/Configurations/PlayerConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> b)
    {
        b.ToTable("players");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.Country).HasMaxLength(2);
        b.HasIndex(x => x.Username).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();

        b.HasOne(x => x.Company)
         .WithOne()
         .HasForeignKey<Company>(c => c.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
'@

Write-File "src/RichLife.Infrastructure/Persistence/Configurations/CompanyConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("companies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.Property(x => x.Cash).HasPrecision(20, 4);
        b.Property(x => x.PassiveIncomePerSecond).HasPrecision(20, 6);
        b.Property(x => x.AllTimeEarnings).HasPrecision(20, 4);
        b.Property(x => x.PrestigeMultiplier).HasPrecision(10, 4);

        b.HasMany(x => x.Businesses)
         .WithOne()
         .HasForeignKey(biz => biz.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Assets)
         .WithOne()
         .HasForeignKey(a => a.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.LuxuryAssets)
         .WithOne()
         .HasForeignKey(a => a.CompanyId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
'@

Write-File "src/RichLife.Infrastructure/Persistence/Configurations/BusinessConfiguration.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RichLife.Domain.Entities;

namespace RichLife.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> b)
    {
        b.ToTable("businesses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.OpeningCost).HasPrecision(20, 4);
        b.Property(x => x.GrossIncomePerSecond).HasPrecision(20, 6);
        b.Property(x => x.MonthlySalaryCost).HasPrecision(20, 4);
        b.Property(x => x.AskingPrice).HasPrecision(20, 4);

        b.HasMany(x => x.Assets)
         .WithOne()
         .HasForeignKey(a => a.BusinessId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
'@

Write-File "src/RichLife.Infrastructure/Repositories/CompanyRepository.cs" @'
using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Entities;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class CompanyRepository(GameDbContext db) : ICompanyRepository
{
    public Task<Company?> GetByPlayerIdAsync(Guid playerId, CancellationToken ct)
        => db.Companies
             .Include(c => c.Businesses).ThenInclude(b => b.Assets)
             .Include(c => c.Assets)
             .Include(c => c.LuxuryAssets)
             .FirstOrDefaultAsync(c => c.PlayerId == playerId, ct);

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Companies
             .Include(c => c.Businesses).ThenInclude(b => b.Assets)
             .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Company company, CancellationToken ct)
        => await db.Companies.AddAsync(company, ct);

    public void Update(Company company)
        => db.Companies.Update(company);
}
'@

Write-File "src/RichLife.Infrastructure/Repositories/PlayerRepository.cs" @'
using Microsoft.EntityFrameworkCore;
using RichLife.Application.Interfaces;
using RichLife.Domain.Entities;
using RichLife.Infrastructure.Persistence;

namespace RichLife.Infrastructure.Repositories;

public class PlayerRepository(GameDbContext db) : IPlayerRepository
{
    public Task<Player?> GetByEmailAsync(string email, CancellationToken ct)
        => db.Players.FirstOrDefaultAsync(p => p.Email == email, ct);

    public Task<Player?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct)
        => db.Players.AnyAsync(p => p.Username == username, ct);

    public async Task AddAsync(Player player, CancellationToken ct)
        => await db.Players.AddAsync(player, ct);
}
'@

Write-File "src/RichLife.Infrastructure/Persistence/UnitOfWork.cs" @'
using RichLife.Application.Interfaces;

namespace RichLife.Infrastructure.Persistence;

public class UnitOfWork(GameDbContext db) : IUnitOfWork
{
    public Task<int> CommitAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
'@

Write-File "src/RichLife.Infrastructure/Events/DomainEventDispatcher.cs" @'
using RichLife.Application.Interfaces;
using RichLife.Domain.Common;

namespace RichLife.Infrastructure.Events;

/// <summary>
/// Simple in-process domain event dispatcher.
/// Register handlers as INotificationHandler<TEvent> and inject here.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        // TODO: dispatch to registered handlers
        // For now: no-op. Extend with MediatR-free handler lookup as needed.
        return Task.CompletedTask;
    }
}
'@

Write-File "src/RichLife.Infrastructure/Extensions/InfrastructureExtensions.cs" @'
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RichLife.Application.Interfaces;
using RichLife.Infrastructure.Events;
using RichLife.Infrastructure.Persistence;
using RichLife.Infrastructure.Repositories;

namespace RichLife.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<GameDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("DefaultConnection"),
                npg => npg.MigrationsAssembly(typeof(GameDbContext).Assembly.GetName().Name)));

        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IPlayerRepository,  PlayerRepository>();
        services.AddScoped<IUnitOfWork,         UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddMemoryCache();

        return services;
    }
}
'@

Ok "Infrastructure layer"

# -- API -----------------------------------------------------------------------
Write-File "src/RichLife.Api/Program.cs" @'
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RichLife.Api.Endpoints;
using RichLife.Application.Extensions;
using RichLife.Infrastructure.Extensions;
using RichLife.ServiceDefaults;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -- Service defaults (Aspire: OpenTelemetry, health checks, resilience) -------
builder.AddServiceDefaults();

// -- Application & Infrastructure ---------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// -- Auth ----------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var cfg = builder.Configuration;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = cfg["Jwt:Issuer"],
            ValidAudience            = cfg["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(cfg["Jwt:Secret"]!))
        };
    });

builder.Services.AddAuthorization();

// -- OpenAPI -------------------------------------------------------------------
builder.Services.AddOpenApi();

// -- CORS ----------------------------------------------------------------------
builder.Services.AddCors(opt =>
    opt.AddPolicy("angular", p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// -- Rate limiting -------------------------------------------------------------
builder.Services.AddRateLimiter(opt =>
{
    opt.AddSlidingWindowLimiter("game-actions", lim =>
    {
        lim.Window            = TimeSpan.FromSeconds(10);
        lim.PermitLimit       = 20;
        lim.SegmentsPerWindow = 5;
    });
});

var app = builder.Build();

app.MapDefaultEndpoints(); // Aspire health endpoints

app.UseCors("angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// -- Endpoints -----------------------------------------------------------------
app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapLeaderboardEndpoints();

app.Run();
'@

Write-File "src/RichLife.Api/Endpoints/AuthEndpoints.cs" @'
using RichLife.Application.DTOs;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.RegisterAsync(req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithName("Register")
        .WithSummary("Register a new player");

        group.MapPost("/login", async (LoginRequest req, AuthService svc, CancellationToken ct) =>
        {
            var result = await svc.LoginAsync(req, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Unauthorized();
        })
        .WithName("Login")
        .WithSummary("Login and receive JWT tokens");
    }
}
'@

Write-File "src/RichLife.Api/Endpoints/GameEndpoints.cs" @'
using System.Security.Claims;
using RichLife.Application.Services;

namespace RichLife.Api.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/game")
            .WithTags("Game")
            .RequireAuthorization();

        // GET /api/game/state  _ load company + apply offline progress
        group.MapGet("/state", async (ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            var playerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result   = await svc.LoadWithOfflineProgressAsync(playerId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        })
        .WithName("GetGameState")
        .WithSummary("Load company state and apply offline earnings");

        // POST /api/game/prestige
        group.MapPost("/prestige", async (ClaimsPrincipal user, CompanyService svc, CancellationToken ct) =>
        {
            var playerId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result   = await svc.PrestigeAsync(playerId, ct);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        })
        .WithName("Prestige")
        .WithSummary("Trigger prestige / IPO");
    }
}
'@

Write-File "src/RichLife.Api/Endpoints/LeaderboardEndpoints.cs" @'
namespace RichLife.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaderboard").WithTags("Leaderboard");

        group.MapGet("/", () => Results.Ok(new { message = "TODO: leaderboard" }))
             .WithName("GetLeaderboard")
             .WithSummary("Top players by net worth, weekly growth, prestige");
    }
}
'@

Write-File "src/RichLife.Api/appsettings.json" @'
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=richlife;Username=richlife;Password=richlife_dev"
  },
  "Jwt": {
    "Secret":   "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_MIN_32_CHARS",
    "Issuer":   "RichLife.Api",
    "Audience": "RichLife.Client"
  },
  "Logging": {
    "LogLevel": {
      "Default":                     "Information",
      "Microsoft.AspNetCore":        "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
'@

Write-File "src/RichLife.Api/appsettings.Development.json" @'
{
  "Logging": {
    "LogLevel": {
      "Default":                     "Debug",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
'@

Ok "API layer"

# -- SERVICE DEFAULTS (Aspire) --------------------------------------------------
Write-File "src/RichLife.ServiceDefaults/Extensions.cs" @'
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace RichLife.ServiceDefaults;

public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(log =>
        {
            log.IncludeFormattedMessage = true;
            log.IncludeScopes           = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlp = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null;
        if (useOtlp) builder.Services.AddOpenTelemetry().UseOtlpExporter();
        return builder;
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });
        return app;
    }
}
'@

Ok "ServiceDefaults (Aspire)"

# -- APP HOST (Aspire orchestrator) ---------------------------------------------
Write-File "src/RichLife.AppHost/Program.cs" @'
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// -- PostgreSQL ----------------------------------------------------------------
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("richlife-pgdata")
    .WithPgAdmin();   // Adds pgAdmin UI on a random port

var richlifeDb = postgres.AddDatabase("richlife");

// -- API -----------------------------------------------------------------------
builder.AddProject<Projects.RichLife_Api>("api")
    .WithReference(richlifeDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
'@

# Fix csproj for AppHost (needs Aspire.Hosting.AppHost SDK)
$appHostCsproj = Join-Path "src/RichLife.AppHost" "RichLife.AppHost.csproj"
Write-File $appHostCsproj @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting" Version="9.*" />
    <PackageReference Include="Aspire.Hosting.PostgreSQL" Version="9.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\RichLife.Api\RichLife.Api.csproj">
      <IsAspireProjectResource>true</IsAspireProjectResource>
    </ProjectReference>
    <ProjectReference Include="..\RichLife.ServiceDefaults\RichLife.ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
'@

Ok "AppHost (Aspire)"

# -- DOCKER --------------------------------------------------------------------
Title "Generating Docker files"

Write-File "docker-compose.yml" @'
version: "3.9"

services:

  postgres:
    image: postgres:17-alpine
    container_name: richlife-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB:       richlife
      POSTGRES_USER:     richlife
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-richlife_dev}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U richlife -d richlife"]
      interval: 10s
      retries: 5

  pgadmin:
    image: dpage/pgadmin4:latest
    container_name: richlife-pgadmin
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      PGADMIN_DEFAULT_EMAIL:    admin@richlife.dev
      PGADMIN_DEFAULT_PASSWORD: ${PGADMIN_PASSWORD:-admin}
    ports:
      - "5050:80"

  api:
    build:
      context: .
      dockerfile: src/RichLife.Api/Dockerfile
    container_name: richlife-api
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT:           Production
      ASPNETCORE_URLS:                  http://+:8080
      ConnectionStrings__DefaultConnection: >-
        Host=postgres;Port=5432;Database=richlife;
        Username=richlife;Password=${POSTGRES_PASSWORD:-richlife_dev}
      Jwt__Secret:   ${JWT_SECRET}
      Jwt__Issuer:   RichLife.Api
      Jwt__Audience: RichLife.Client
    ports:
      - "8080:8080"

volumes:
  pgdata:
'@

Write-File ".env" @'
# Copy this to .env and fill in production values
POSTGRES_PASSWORD=richlife_dev
PGADMIN_PASSWORD=admin
JWT_SECRET=CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS
'@

Write-File "src/RichLife.Api/Dockerfile" @'
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RichLife.Api/RichLife.Api.csproj",             "src/RichLife.Api/"]
COPY ["src/RichLife.Application/RichLife.Application.csproj",   "src/RichLife.Application/"]
COPY ["src/RichLife.Domain/RichLife.Domain.csproj",       "src/RichLife.Domain/"]
COPY ["src/RichLife.Infrastructure/RichLife.Infrastructure.csproj","src/RichLife.Infrastructure/"]
COPY ["src/RichLife.ServiceDefaults/RichLife.ServiceDefaults.csproj","src/RichLife.ServiceDefaults/"]

RUN dotnet restore "src/RichLife.Api/RichLife.Api.csproj"

COPY . .
WORKDIR "/src/src/RichLife.Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RichLife.Api.dll"]
'@

Write-File ".dockerignore" @'
**/.git
**/obj
**/bin
**/.vs
**/node_modules
**/.env
'@

Ok "Docker files"

# -- MIGRATIONS HELPER SCRIPT --------------------------------------------------
Write-File "scripts/Add-Migration.ps1" @'
#!/usr/bin/env pwsh
param([Parameter(Mandatory)][string]$Name)

# Run from solution root
dotnet ef migrations add $Name `
    --project src/RichLife.Infrastructure `
    --startup-project src/RichLife.Api `
    --output-dir Persistence/Migrations

Write-Host "Migration '$Name' created." -ForegroundColor Green
Write-Host "Run  ./scripts/Update-Database.ps1  to apply." -ForegroundColor Cyan
'@

Write-File "scripts/Update-Database.ps1" @'
#!/usr/bin/env pwsh
dotnet ef database update `
    --project src/RichLife.Infrastructure `
    --startup-project src/RichLife.Api
Write-Host "Database updated." -ForegroundColor Green
'@

Write-File "scripts/Drop-Database.ps1" @'
#!/usr/bin/env pwsh
Write-Host "WARNING: This will drop the database. Continue? (y/N): " -NoNewline -ForegroundColor Red
if ((Read-Host) -eq 'y') {
    dotnet ef database drop --force `
        --project src/RichLife.Infrastructure `
        --startup-project src/RichLife.Api
}
'@

Ok "Migration scripts"

# -- .gitignore ----------------------------------------------------------------
Write-File ".gitignore" @'
obj/
bin/
.vs/
*.user
.env
*.pfx
**/Migrations/
'@

Write-File "README.md" @"
# Rich Life _ Backend

## Requirements
- .NET 10 SDK
- Docker Desktop

## Quick start (dev)

\`\`\`bash
# 1. Start PostgreSQL
docker compose up postgres -d

# 2. Run API (with Aspire dashboard)
dotnet run --project src/RichLife.AppHost

# 3. First migration
./scripts/Add-Migration.ps1 -Name InitialCreate
./scripts/Update-Database.ps1
\`\`\`

## URLs (dev)
| Service           | URL                            |
|-------------------|-------------------------------|
| API               | https://localhost:7xxx         |
| Scalar (OpenAPI)  | https://localhost:7xxx/scalar  |
| Aspire Dashboard  | http://localhost:18888         |
| pgAdmin           | http://localhost:5050          |

## Useful commands

\`\`\`bash
# Add a migration
./scripts/Add-Migration.ps1 -Name YourMigrationName

# Apply migrations
./scripts/Update-Database.ps1

# Full docker stack
docker compose up -d
docker compose down -v   # removes volumes too

# Build production image
docker build -f src/RichLife.Api/Dockerfile -t richlife-api .
\`\`\`
"@

# -- Final summary -------------------------------------------------------------
Title "Done!"
Write-Host @"

  Solution created at: $SolDir

  Next steps:
    1.  cd $SolDir
    2.  docker compose up postgres -d
    3.  dotnet run --project src/RichLife.AppHost
        _ Aspire Dashboard : http://localhost:18888
        _ API              : https://localhost:7xxx/scalar
    4.  ./scripts/Add-Migration.ps1 -Name InitialCreate
    5.  ./scripts/Update-Database.ps1

"@ -ForegroundColor Cyan
