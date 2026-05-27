namespace NomadVaultMySqlDotnet.Models;

public sealed class CustomerRecord
{
    public int CustomerNumber { get; set; }
    public string BirthDate { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CreateDate { get; set; } = string.Empty;
    public string Ssn { get; set; } = string.Empty;
    public string Ccn { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
}
