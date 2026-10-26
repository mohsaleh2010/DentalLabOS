using DentalLabOS.Domain.Common;

namespace DentalLabOS.Domain.Parties;

/// <summary>A clinic, center or independent dentist the lab works for (plan B4, A2.2).</summary>
public class Customer : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public PartyType Type { get; set; }
    public Guid? GroupId { get; set; }
    public CustomerGroup? Group { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public int PaymentTermsDays { get; set; }

    /// <summary>0 means no limit.</summary>
    public decimal CreditLimit { get; set; }
    public bool BlockWhenOverCredit { get; set; }
    public Guid? PriceListId { get; set; }

    /// <summary>Receivables account; set when accounting (phase 11) maps it.</summary>
    public Guid? ReceivableAccountId { get; set; }

    /// <summary>Becomes an opening journal entry in phase 11 and is locked once posted.</summary>
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Never shown in the dentist portal.</summary>
    public string? InternalNotes { get; set; }
    public ICollection<Dentist> Dentists { get; set; } = new List<Dentist>();
    public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
}

public class CustomerGroup : TenantEntity
{
    public string Name { get; set; } = default!;
    public Guid? DefaultPriceListId { get; set; }
}

public class CustomerContact : TenantEntity
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = default!;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class Dentist : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Specialty { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>Portal account, created in phase 12.</summary>
    public Guid? PortalUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier : TenantEntity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public int PaymentTermsDays { get; set; }

    /// <summary>Payables account; set when accounting (phase 11) maps it.</summary>
    public Guid? PayableAccountId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? Rating { get; set; }

    /// <summary>Provides outsourced lab services rather than goods.</summary>
    public bool IsOutsourcePartner { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<SupplierItem> Items { get; set; } = new List<SupplierItem>();
}

public class SupplierItem : TenantEntity
{
    public Guid SupplierId { get; set; }
    public Guid ItemId { get; set; }
    public Catalog.Item Item { get; set; } = default!;
    public string? SupplierSku { get; set; }
    public decimal LastPrice { get; set; }
    public string CurrencyCode { get; set; } = "EGP";
    public int LeadTimeDays { get; set; }
    public decimal MinOrderQty { get; set; }
    public bool IsPreferred { get; set; }
}
