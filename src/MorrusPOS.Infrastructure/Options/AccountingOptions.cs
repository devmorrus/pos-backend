namespace MorrusPOS.Infrastructure.Options;

public class AccountingOptions
{
    public const string SectionName = "Accounting";

    // When enabled, keyword account resolution is disabled and every posting
    // requires an explicit accounting_account_mappings row.
    public bool StrictMappings { get; set; }
}
