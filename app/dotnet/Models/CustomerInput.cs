using Microsoft.AspNetCore.Mvc;

namespace NomadVaultMySqlDotnet.Models;

public sealed class CustomerInput
{
    [FromForm(Name = "cust_no")]
    public int? CustNo { get; set; }

    [FromForm(Name = "birth_date")]
    public string BirthDate { get; set; } = string.Empty;

    [FromForm(Name = "first_name")]
    public string FirstName { get; set; } = string.Empty;

    [FromForm(Name = "last_name")]
    public string LastName { get; set; } = string.Empty;

    [FromForm(Name = "create_date")]
    public string CreateDate { get; set; } = string.Empty;

    [FromForm(Name = "ssn")]
    public string Ssn { get; set; } = string.Empty;

    [FromForm(Name = "ccn")]
    public string Ccn { get; set; } = string.Empty;

    [FromForm(Name = "address")]
    public string Address { get; set; } = string.Empty;

    [FromForm(Name = "salary")]
    public string Salary { get; set; } = string.Empty;
}
