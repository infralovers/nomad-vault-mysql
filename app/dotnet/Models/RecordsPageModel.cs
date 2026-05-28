namespace NomadVaultMySqlDotnet.Models;

public sealed class RecordsPageModel
{
    public IReadOnlyList<CustomerRecord> Results { get; set; } = [];
    public bool RecordAdded { get; set; }
    public bool RecordUpdated { get; set; }
}
